using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.NPC.Systems;
using Content.Server.Objectives;
using Content.Server.PDA.Ringer;
using Content.Server.Roles;
using Content.Server.Shuttles.Components;
using Content.Server.Traitor.Uplink;
using Content.Shared.CCVar;
using Content.Shared.Dataset;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Rules;

public sealed class BrokenAiRuleSystem : GameRuleSystem<BrokenAiRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly UplinkSystem _uplink = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    private int PlayersPerTraitor => _cfg.GetCVar(CCVars.TraitorPlayersPerTraitor);
    private int MaxTraitors => _cfg.GetCVar(CCVars.TraitorMaxTraitors);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayersSpawned);

        SubscribeLocalEvent<TraitorRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
        SubscribeLocalEvent<TraitorRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
    }

    private void OnPlayersSpawned(RulePlayerJobsAssignedEvent ev)
    {
        var query = EntityQueryEnumerator<TraitorRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var traitor, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;
            foreach (var player in ev.Players)
            {
                if (!ev.Profiles.ContainsKey(player.UserId))
                    continue;
            }
        }
    }

    public bool MakeTraitor(ICommonSession traitor, bool giveUplink = true, bool giveObjectives = true)
    {
        var traitorRule = EntityQuery<TraitorRuleComponent>().FirstOrDefault();
        if (traitorRule == null)
        {
            //todo fuck me this shit is awful
            //no i wont fuck you, erp is against rules
            GameTicker.StartGameRule("Traitor", out var ruleEntity);
            traitorRule = Comp<TraitorRuleComponent>(ruleEntity);
        }

        if (!_mindSystem.TryGetMind(traitor, out var mindId, out var mind))
        {
            Log.Info("Failed getting mind for picked traitor.");
            return false;
        }

        if (HasComp<TraitorRoleComponent>(mindId))
        {
            Log.Error($"Player {traitor.Name} is already a traitor.");
            return false;
        }

        if (mind.OwnedEntity is not { } entity)
        {
            Log.Error("Mind picked for traitor did not have an attached entity.");
            return false;
        }

        // Calculate the amount of currency on the uplink.
        var startingBalance = _cfg.GetCVar(CCVars.TraitorStartingBalance);
        if (_jobs.MindTryGetJob(mindId, out _, out var prototype))
            startingBalance = Math.Max(startingBalance - prototype.AntagAdvantage, 0);

        // Give traitors their codewords and uplink code to keep in their character info menu
        var briefing = Loc.GetString("traitor-role-codewords-short", ("codewords", string.Join(", ", traitorRule.Codewords)));
        Note[]? code = null;
        if (giveUplink)
        {
            // creadth: we need to create uplink for the antag.
            // PDA should be in place already
            var pda = _uplink.FindUplinkTarget(mind.OwnedEntity!.Value);
            if (pda == null || !_uplink.AddUplink(mind.OwnedEntity.Value, startingBalance))
                return false;

            // Give traitors their codewords and uplink code to keep in their character info menu
            code = EnsureComp<RingerUplinkComponent>(pda.Value).Code;
            // If giveUplink is false the uplink code part is omitted
            briefing = string.Format("{0}\n{1}", briefing,
                Loc.GetString("traitor-role-uplink-code-short", ("code", string.Join("-", code).Replace("sharp","#"))));
        }

        // Prepare traitor role
        var traitorRole = new TraitorRoleComponent
        {
            PrototypeId = traitorRule.TraitorPrototypeId,
        };

        // Assign traitor roles
        _roleSystem.MindAddRole(mindId, new TraitorRoleComponent
        {
            PrototypeId = traitorRule.TraitorPrototypeId
        }, mind);
        // Assign briefing and greeting sound
        _roleSystem.MindAddRole(mindId, new RoleBriefingComponent
        {
            Briefing = briefing
        }, mind);
        _roleSystem.MindPlaySound(mindId, traitorRule.GreetSoundNotification, mind);
        SendTraitorBriefing(mindId, traitorRule.Codewords, code);
        traitorRule.TraitorMinds.Add(mindId);

        // Change the faction
        _npcFaction.RemoveFaction(entity, "NanoTrasen", false);
        _npcFaction.AddFaction(entity, "Syndicate");

        // Give traitors their objectives
        if (giveObjectives)
        {
            var maxDifficulty = _cfg.GetCVar(CCVars.TraitorMaxDifficulty);
            var maxPicks = _cfg.GetCVar(CCVars.TraitorMaxPicks);
            var difficulty = 0f;
            for (var pick = 0; pick < maxPicks && maxDifficulty > difficulty; pick++)
            {
                var objective = _objectives.GetRandomObjective(mindId, mind, "TraitorObjectiveGroups");
                if (objective == null)
                    continue;

                _mindSystem.AddObjective(mindId, mind, objective.Value);
                difficulty += Comp<ObjectiveComponent>(objective.Value).Difficulty;
            }
        }

        return true;
    }

    /// <summary>
    ///     Send a codewords and uplink codes to traitor chat.
    /// </summary>
    /// <param name="mind">A mind (player)</param>
    /// <param name="codewords">Codewords</param>
    /// <param name="code">Uplink codes</param>
    private void SendTraitorBriefing(EntityUid mind, string[] codewords, Note[]? code)
    {
        if (!_mindSystem.TryGetSession(mind, out var session))
            return;

        _chatManager.DispatchServerMessage(session, Loc.GetString("traitor-role-greeting"));
        _chatManager.DispatchServerMessage(session, Loc.GetString("traitor-role-codewords", ("codewords", string.Join(", ", codewords))));
        if (code != null)
            _chatManager.DispatchServerMessage(session, Loc.GetString("traitor-role-uplink-code", ("code", string.Join("-", code).Replace("sharp","#"))));
    }

    private void OnObjectivesTextGetInfo(EntityUid uid, TraitorRuleComponent comp, ref ObjectivesTextGetInfoEvent args)
    {
        args.Minds = comp.TraitorMinds;
        args.AgentName = Loc.GetString("traitor-round-end-agent-name");
    }

    private void OnObjectivesTextPrepend(EntityUid uid, TraitorRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        args.Text += "\n" + Loc.GetString("traitor-round-end-codewords", ("codewords", string.Join(", ", comp.Codewords)));
    }
}

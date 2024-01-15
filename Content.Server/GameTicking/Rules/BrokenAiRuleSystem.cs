using System.Linq;
using Content.Server.Administration.Managers;
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
using Content.Shared.Administration;
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
    [Dependency] private readonly IAdminManager _adminManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleSpawn);

        SubscribeLocalEvent<BrokenAiRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
        SubscribeLocalEvent<BrokenAiRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
    }

    private void HandleSpawn(PlayerSpawnCompleteEvent ev)
    {
        var query = EntityQueryEnumerator<BrokenAiRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (component.BrokenAi != null)
                return;
            if (!ev.Profile.AntagPreferences.Contains(component.BrokenAiPrototypeId))
                return;

            if (ev.JobId is "SAI")
            {
                BreakAi(ev.Player, component);
            }
        }
    }

    private void BreakAi(ICommonSession evPlayer, BrokenAiRuleComponent component)
    {
        if (!_mindSystem.TryGetMind(evPlayer, out var mindId, out var mind))
        {
            Log.Info("Failed getting mind for picked traitor.");
            return;
        }

        if (!_adminManager.HasAdminFlag(evPlayer, AdminFlags.BrokenAi))
            return;

        if (HasComp<BrokenAiRoleComponent>(mindId))
        {
            Log.Error("AI mind already has broken ai component!");
            return;
        }

        if (mind.OwnedEntity is not { } entity)
        {
            Log.Error("Mind picked for traitor did not have an attached entity.");
            return;
        }

        var brokenAiRole = new BrokenAiRoleComponent()
        {
            PrototypeId = component.BrokenAiPrototypeId,
        };

        _roleSystem.MindAddRole(mindId, brokenAiRole, mind);
        _roleSystem.MindPlaySound(mindId, component.GreetSoundNotification, mind);

        SendBrokenAiBriefing(mindId);

        component.BrokenAi = entity;

        _npcFaction.RemoveFaction(entity, "NanoTrasen", false);
    }

    private void SendBrokenAiBriefing(EntityUid mindId)
    {
        if (!_mindSystem.TryGetSession(mindId, out var session))
            return;

        _chatManager.DispatchServerMessage(session, Loc.GetString("broken-ai-role-greeting"));
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

    private void OnObjectivesTextGetInfo(EntityUid uid, BrokenAiRuleComponent comp, ref ObjectivesTextGetInfoEvent args)
    {
        //args.Minds = comp.TraitorMinds;
        //args.AgentName = Loc.GetString("traitor-round-end-agent-name");

        if (comp.BrokenAi != null)
            args.Minds = new List<EntityUid>() { comp.BrokenAi.Value };
        args.AgentName = Loc.GetString("broken-ai-round-end-agent-name");
    }

    private void OnObjectivesTextPrepend(EntityUid uid, BrokenAiRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        //args.Text += "\n" + Loc.GetString("traitor-round-end-codewords", ("codewords", string.Join(", ", comp.Codewords)));
    }
}

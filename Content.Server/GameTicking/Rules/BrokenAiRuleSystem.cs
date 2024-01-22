using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.NPC.Systems;
using Content.Server.Objectives;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Shared.Administration;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

public sealed class BrokenAiRuleSystem : GameRuleSystem<BrokenAiRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleSpawn);

        SubscribeLocalEvent<BrokenAiComponent, ComponentStartup>(OnRoleStart);

        SubscribeLocalEvent<BrokenAiRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
        SubscribeLocalEvent<BrokenAiRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
    }

    private void OnRoleStart(EntityUid uid, BrokenAiComponent component, ComponentStartup args)
    {
        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var transmitterComponent))
        {
            transmitterComponent.Channels.Add(component.BrokenAiSyndicateChannel);

            Dirty(uid, transmitterComponent);
        }

        if (TryComp<ActiveRadioComponent>(uid, out var radio))
        {
            radio.Channels.Add(component.BrokenAiSyndicateChannel);

            Dirty(uid, radio);
        }
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
                BreakAi(ev.Player, component, ev.Mob);
            }
        }
    }

    private void BreakAi(ICommonSession evPlayer, BrokenAiRuleComponent component, EntityUid aiEnt)
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

        var brokenAiBriefing = new RoleBriefingComponent()
        {
            Briefing = Loc.GetString("broken-ai-briefing")
        };

        _roleSystem.MindAddRole(mindId, brokenAiRole, mind);
        _roleSystem.MindAddRole(mindId, brokenAiBriefing, mind);
        _roleSystem.MindPlaySound(mindId, component.GreetSoundNotification, mind);

        AddComp<BrokenAiComponent>(aiEnt);

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

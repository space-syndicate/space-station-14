using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Server.Silicons.Laws;
using Content.Server.Silicons.Malfunction;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles.Components;
using Content.Shared.Silicons.Malfunction;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Handles turning the selected station AI player into a Malfunction AI antagonist:
/// swaps their laws, marks them subverted, and shows the antagonist briefing.
/// </summary>
public sealed partial class MalfunctionAiRuleSystem : GameRuleSystem<MalfunctionAiRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CodeConditionSystem _codeCondition = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SiliconLawSystem _law = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfunctionAiRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);

        SubscribeLocalEvent<MalfunctionAiRoleComponent, GetBriefingEvent>(OnGetBriefing);

        SubscribeLocalEvent<MalfDoomsdayArmedEvent>(OnDoomsdayArmed);
    }

    private void OnDoomsdayArmed(ref MalfDoomsdayArmedEvent args)
    {
        // Find an active malf rule and arm its Doomsday timer.
        var query = EntityQueryEnumerator<MalfunctionAiRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rule, out _))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid))
                continue;

            if (rule.DoomsdayArmed)
                continue;

            rule.DoomsdayArmed = true;
            rule.DoomsdayAi = args.Ai;

            var station = _station.GetOwningStation(args.Ai);
            if (station != null)
            {
                _chat.DispatchStationAnnouncement(
                    station.Value,
                    Loc.GetString("malfunction-ai-announcement-doomsday-armed",
                        ("time", (int) rule.DoomsdayRemaining)),
                    Loc.GetString("malfunction-ai-announcement-sender"),
                    playDefaultSound: true,
                    colorOverride: Color.Red);
            }

            return;
        }
    }

    protected override void ActiveTick(EntityUid uid, MalfunctionAiRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (!component.DoomsdayArmed || component.DoomsdayDetonated)
            return;

        component.DoomsdayRemaining -= frameTime;

        // Threshold announcements.
        if (component.DoomsdayAnnouncementsLeft.Count > 0)
        {
            var next = component.DoomsdayAnnouncementsLeft[0];
            if (component.DoomsdayRemaining <= next)
            {
                component.DoomsdayAnnouncementsLeft.RemoveAt(0);
                AnnounceDoomsday(component, next);
            }
        }

        if (component.DoomsdayRemaining > 0f)
            return;

        Detonate((uid, component));
    }

    private void AnnounceDoomsday(MalfunctionAiRuleComponent component, int secondsLeft)
    {
        if (component.DoomsdayAi is not { } ai)
            return;

        var station = _station.GetOwningStation(ai);
        if (station == null)
            return;

        _chat.DispatchStationAnnouncement(
            station.Value,
            Loc.GetString("malfunction-ai-announcement-doomsday-tick", ("time", secondsLeft)),
            Loc.GetString("malfunction-ai-announcement-sender"),
            playDefaultSound: true,
            colorOverride: Color.Red);
    }

    private void Detonate(Entity<MalfunctionAiRuleComponent> ent)
    {
        ent.Comp.DoomsdayDetonated = true;

        if (ent.Comp.DoomsdayAi is not { } ai || !Exists(ai))
            return;

        _explosion.QueueExplosion(
            ai,
            ent.Comp.DoomsdayExplosionType,
            ent.Comp.DoomsdayExplosionIntensity,
            ent.Comp.DoomsdayExplosionSlope,
            ent.Comp.DoomsdayMaxTileIntensity,
            canCreateVacuum: true,
            user: ai);

        // Mark the Doomsday objective as completed for the AI's mind.
        _codeCondition.SetCompleted(ai, "MalfunctionAiDoomsdayObjective");
    }

    private void AfterAntagSelected(Entity<MalfunctionAiRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var aiUid = args.EntityUid;

        // Free the AI from its original laws.
        _law.SetSubvertedLawset(aiUid, ent.Comp.Lawset);

        // Attach the malf component which grants the malf actions.
        EnsureComp<MalfunctionAiComponent>(aiUid);

        _antag.SendBriefing(aiUid, MakeBriefing(), Color.Red, ent.Comp.GreetSound);
    }

    // Character screen briefing.
    private void OnGetBriefing(Entity<MalfunctionAiRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Append(MakeBriefing());
    }

    private string MakeBriefing()
    {
        return Loc.GetString("malfunction-ai-role-greeting");
    }
}

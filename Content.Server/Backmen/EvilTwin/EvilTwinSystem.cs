using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.DetailExaminable;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Humanoid;
using Content.Server.Jobs;
using Content.Server.Mind.Components;
using Content.Server.Objectives;
using Content.Server.Players;
using Content.Server.Prayer;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Server.Traitor;
using Content.Server.Traits.Assorted;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.EvilTwin;

public sealed class EvilTwinSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvilTwinSpawnerComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<EvilTwinComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
    }

    private void OnPlayerAttached(EntityUid uid, EvilTwinSpawnerComponent component, PlayerAttachedEvent args)
    {
        if (TryGetEligibleHumanoid(out var targetUid))
        {
            var xform = Transform(uid);
            var twinMob = SpawnEvilTwin(targetUid.Value, xform.Coordinates);
            if (twinMob != null)
            {
                var playerData = args.Player.ContentData();
                if (playerData != null)
                {
                    var mind = playerData.Mind;
                    if (mind != null)
                    {
                        mind.TransferTo(twinMob);
                    }
                }
            }
        }
        else
        {
                _prayerSystem.SendSubtleMessage(args.Player, args.Player, Loc.GetString("evil-twin-error-message"),  Loc.GetString("prayer-popup-subtle-default"));
        }

        QueueDel(uid);
    }

    private void OnMindAdded(EntityUid uid, EvilTwinComponent component, MindAddedMessage args)
    {
        if (!TryComp<MindComponent>(uid, out var mindComponent) || mindComponent.Mind == null)
        {
            return;
        }

        var mind = mindComponent.Mind;
        mind.AddRole(new TraitorRole(mind, _prototype.Index<AntagPrototype>(EvilTwinRole)));
        mind.TryAddObjective(_prototype.Index<ObjectivePrototype>(KillObjective));
        mind.TryAddObjective(_prototype.Index<ObjectivePrototype>(EscapeObjective));

        RemComp<PacifistComponent>(uid);
        RemComp<PacifiedComponent>(uid);
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        var twins = EntityQuery<EvilTwinComponent, MindComponent>()
            .ToList();
        if (twins.Count < 1)
        {
            return;
        }

        var result = Loc.GetString("evil-twin-round-end-result", new ValueTuple<string, object>("evil-twin-count", twins.Count));
        foreach (var fugi in twins)
        {
            if (fugi.Item2.Mind == null)
                continue;
            var name = fugi.Item2.Mind.CharacterName;
            fugi.Item2.Mind.TryGetSession(out var session);
            var username = session?.Name;
            var objectives = fugi.Item2.Mind.AllObjectives.ToArray();
            if (objectives.Length == 0)
            {
                if (username != null)
                {
                    if (name == null)
                    {
                        result = result + "\n" + Loc.GetString("evil-twin-user-was-an-evil-twin", new ValueTuple<string, object>("user", username));
                    }
                    else
                    {
                        result = result + "\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-named", new ValueTuple<string, object>("user", username), new ValueTuple<string, object>("name", name));
                    }
                }
                else if (name != null)
                {
                    result = result + "\n" + Loc.GetString("evil-twin-was-an-evil-twin-named", new ValueTuple<string, object>("name", name));
                }
            }
            else
            {
                if (username != null)
                {
                    if (name == null)
                    {
                        result = result + "\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-with-objectives", new ValueTuple<string, object>("user", username));
                    }
                    else
                    {
                        result = result + "\n" + Loc.GetString(
                            "evil-twin-user-was-an-evil-twin-with-objectives-named", new ValueTuple<string, object>("user", username), new ValueTuple<string, object>("name", name));
                    }
                }
                else if (name != null)
                {
                    result = result + "\n" + Loc.GetString("evil-twin-was-an-evil-twin-with-objectives-named", new ValueTuple<string, object>("name", name));
                }

                foreach (IGrouping<string, Objective> grouping in from o in objectives
                         group o by o.Prototype.Issuer)
                {
                    foreach (var objective in grouping)
                    {
                        foreach (var condition in objective.Conditions)
                        {
                            var progress = condition.Progress;
                            if (progress > 0.99f)
                            {
                                result = result + "\n- " + Loc.GetString("traitor-objective-condition-success", new ValueTuple<string, object>("condition", condition.Title), new ValueTuple<string, object>("markupColor", "green"));
                            }
                            else
                            {
                                result = result + "\n- " + Loc.GetString("traitor-objective-condition-fail", new ValueTuple<string, object>("condition", condition.Title), new ValueTuple<string, object>("progress", (int) (progress * 100f)), new ValueTuple<string, object>("markupColor", "red"));
                            }
                        }
                    }
                }
            }
        }

        ev.AddLine(result);
    }

    private bool TryGetEligibleHumanoid([NotNullWhen(true)] out EntityUid? uid)
    {
        var targets = EntityQuery<ActorComponent, MindComponent, HumanoidAppearanceComponent>()
            .ToList();
        _random.Shuffle(targets);
        foreach (var target in targets)
        {
            var mind = target.Item2.Mind;
            if (mind?.CurrentJob != null)
            {
                var mind2 = target.Item2.Mind;
                if (mind2 is { CurrentEntity: { } })
                {
                    var targetUid = target.Item2.Mind?.CurrentEntity!.Value;
                    if (targetUid != null && !HasComp<EvilTwinComponent>(targetUid) &&
                        !HasComp<NukeOperativeComponent>(targetUid))
                    {
                        uid = new EntityUid?(targetUid.Value);
                        return true;
                    }
                }
            }
        }

        uid = null;
        return false;
    }

    private EntityUid? SpawnEvilTwin(EntityUid target, EntityCoordinates coords)
    {
        if (!TryComp<MindComponent>(target, out var mind) ||
            !TryComp<HumanoidAppearanceComponent>(target, out var humanoid) ||
            !TryComp<ActorComponent>(target, out var actor) ||
            !_prototype.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
        {
            return null;
        }

        var pref = (HumanoidCharacterProfile) _prefs.GetPreferences(actor.PlayerSession.UserId).SelectedCharacter;
        var twinUid = Spawn(species.Prototype, coords);
        _humanoid.LoadProfile(twinUid, pref);
        MetaData(twinUid).EntityName = MetaData(target).EntityName;
        if (TryComp<DetailExaminableComponent>(target, out var detail))
        {
            EnsureComp<DetailExaminableComponent>(twinUid).Content = detail.Content;
        }

        var mind2 = mind.Mind!;
        bool flag;
        {
            var currentJob = mind2.CurrentJob;
            flag = (currentJob?.StartingGear != null);
        }
        if (flag)
        {
            if (_prototype.TryIndex<StartingGearPrototype>(mind2.CurrentJob!.StartingGear!, out var gear))
            {
                _stationSpawning.EquipStartingGear(twinUid, gear, pref);
                _stationSpawning.EquipIdCard(twinUid, pref.Name, mind2.CurrentJob.Prototype,
                    _stationSystem.GetOwningStation(target));
            }

            foreach (var special in mind2.CurrentJob.Prototype.Special)
            {
                if (special is AddComponentSpecial)
                {
                    special.AfterEquip(twinUid);
                }
            }
        }

        EnsureComp<EvilTwinComponent>(twinUid).TwinMind = mind?.Mind;
        return new EntityUid?(twinUid);
    }

    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;

    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    [Dependency] private readonly StationSystem _stationSystem = default!;

    [Dependency] private readonly PrayerSystem _prayerSystem = default!;

    private const string EvilTwinRole = "EvilTwin";

    private const string KillObjective = "KillObjectiveEvilTwin";

    private const string EscapeObjective = "EscapeShuttleObjectiveEvilTwin";
}

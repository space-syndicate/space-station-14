using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.DetailExaminable;
using Content.Server.GameTicking;
using Content.Server.Humanoid;
using Content.Server.Jobs;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.Station.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Corvax.EvilTwin;

public sealed class EvilTwinSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly JobSystem _jobSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;

    [ValidatePrototypeId<AntagPrototype>]
    private const string EvilTwinRole = "EvilTwin";
    [ValidatePrototypeId<EntityPrototype>]
    private const string KillObjective = "KillTwinObjective";
    [ValidatePrototypeId<EntityPrototype>]
    private const string EscapeObjective = "EscapeShuttleTwinObjective";

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
            var spawnerCoords = Transform(uid).Coordinates;
            var spawnedTwin = TrySpawnEvilTwin(targetUid.Value, spawnerCoords);
            if (spawnedTwin != null &&
                _mindSystem.TryGetMind(args.Player, out var mindId, out var mind))
            {
                mind.CharacterName = MetaData(spawnedTwin.Value).EntityName;
                _mindSystem.TransferTo(mindId, spawnedTwin);
            }
        }

        QueueDel(uid);
    }

    private void OnMindAdded(EntityUid uid, EvilTwinComponent component, MindAddedMessage args)
    {
        if (!TryComp<EvilTwinComponent>(uid, out var evilTwin) ||
            !_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;

        var role = new TraitorRoleComponent
        {
            PrototypeId = EvilTwinRole,
        };
        _roleSystem.MindAddRole(mindId, role, mind);
        _mindSystem.TryAddObjective(mindId, mind, EscapeObjective);
        _mindSystem.TryAddObjective(mindId, mind, KillObjective);
        if (TryComp<TargetObjectiveComponent>(uid, out var targetObj))
            _target.SetTarget(uid, evilTwin.TargetMindId, targetObj);
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        var twinsCount = EntityQuery<EvilTwinComponent>().Count();
        if (twinsCount == 0)
            return;

        var result = Loc.GetString("evil-twin-round-end-result", ("evil-twin-count", twinsCount));

        var query = EntityQueryEnumerator<EvilTwinComponent>();
        while (query.MoveNext(out var uid, out var twin))
        {
            if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
                continue;

            var name = mind.CharacterName;
            _mindSystem.TryGetSession(mind.OwnedEntity, out var session);
            var username = session?.Name;

            var objectives = mind.Objectives.ToArray();
            if (objectives.Length == 0)
            {
                if (username != null)
                {
                    if (name == null)
                        result += "\n" + Loc.GetString("evil-twin-user-was-an-evil-twin", ("user", username));
                    else
                        result += "\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-named", ("user", username), ("name", name));
                }
                else if (name != null)
                    result += "\n" + Loc.GetString("evil-twin-was-an-evil-twin-named", ("name", name));

                continue;
            }

            if (username != null)
            {
                if (name == null)
                    result += "\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-with-objectives", ("user", username));
                else
                    result += "\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-with-objectives-named", ("user", username), ("name", name));
            }
            else if (name != null)
                result += "\n" + Loc.GetString("evil-twin-was-an-evil-twin-with-objectives-named", ("name", name));

            foreach (var objectiveGroup in objectives.GroupBy(o => Comp<ObjectiveComponent>(o).Issuer))
            {
                foreach (var objective in objectiveGroup)
                {
                    var info = _objectives.GetInfo(objective, mindId, mind);
                    if (info == null)
                        continue;

                    var objectiveTitle = info.Value.Title;
                    var progress = info.Value.Progress;
                    if (progress > 0.99f)
                    {
                        result += "\n- " + Loc.GetString(
                            "objectives-objective-success",
                            ("objective", objectiveTitle),
                            ("markupColor", "green")
                        );
                    }
                    else
                    {
                        result += "\n- " + Loc.GetString(
                            "objectives-objective-fail",
                            ("objective", objectiveTitle),
                            ("progress", (int) (progress * 100)),
                            ("markupColor", "red")
                        );
                    }
                }
            }
        }
        ev.AddLine(result);
    }

    /// <summary>
    ///     Get first random humanoid controlled by player mob with job
    /// </summary>
    /// <param name="uid">Found humanoid uid</param>
    /// <returns>false if not found</returns>
    private bool TryGetEligibleHumanoid([NotNullWhen(true)] out EntityUid? uid)
    {
        var targets = EntityQuery<ActorComponent, HumanoidAppearanceComponent>().ToList();
        _random.Shuffle(targets);
        foreach (var (actor, _) in targets)
        {
            if (!_mindSystem.TryGetMind(actor.PlayerSession, out var mindId, out var mind) || mind.OwnedEntity == null)
                continue;

            if (!_jobSystem.MindTryGetJob(mindId, out _, out _))
                continue;

            // There was check for nukeops or evil twin, but ist it will be fun?

            uid = mind.OwnedEntity;
            return true;
        }

        uid = null;
        return false;
    }

    /// <summary>
    ///     Spawns "clone" in round start state of target human mob
    /// </summary>
    /// <param name="target">Target for cloning</param>
    /// <param name="coords">Spawn location</param>
    /// <returns>null if target in invalid state (ghost, leave, ...)</returns>
    private EntityUid? TrySpawnEvilTwin(EntityUid target, EntityCoordinates coords)
    {
        if (!_mindSystem.TryGetMind(target, out var mindId, out _) ||
            !TryComp<HumanoidAppearanceComponent>(target, out var humanoid) ||
            !TryComp<ActorComponent>(target, out var actor) ||
            !_prototype.TryIndex(humanoid.Species, out var species))
            return null;

        var pref = (HumanoidCharacterProfile) _prefs.GetPreferences(actor.PlayerSession.UserId).SelectedCharacter;

        var twinUid = Spawn(species.Prototype, coords);
        _humanoid.LoadProfile(twinUid, pref);
        _metaDataSystem.SetEntityName(twinUid, MetaData(target).EntityName);
        if (TryComp<DetailExaminableComponent>(target, out var detail))
        {
            var detailCopy = EnsureComp<DetailExaminableComponent>(twinUid);
            detailCopy.Content = detail.Content;
        }

        if (_jobSystem.MindTryGetJob(mindId, out _, out var jobProto) && jobProto.StartingGear != null)
        {
            if (_prototype.TryIndex<StartingGearPrototype>(jobProto.StartingGear, out var gear))
            {
                _stationSpawning.EquipStartingGear(twinUid, gear);
                _stationSpawning.SetPdaAndIdCardData(twinUid, pref.Name, jobProto, _stationSystem.GetOwningStation(target));
            }

            foreach (var special in jobProto.Special)
            {
                if (special is AddComponentSpecial)
                    special.AfterEquip(twinUid);
            }
        }

        EnsureComp<EvilTwinComponent>(twinUid).TargetMindId = mindId;

        return twinUid;
    }
}


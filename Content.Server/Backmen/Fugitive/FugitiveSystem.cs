using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Content.Server.Mind;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Objectives;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.Paper;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Server.Roles;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Storage.Components;
using Content.Shared.Roles;
using Content.Shared.Movement.Systems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Paper;
using Content.Shared.Radio.Components;
using Content.Shared.Random;
using Content.Shared.Roles.Jobs;
using Content.Shared.Wall;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Random;
using static Content.Shared.Examine.ExamineSystemShared;

namespace Content.Server.Backmen.Fugitive;

public sealed class FugitiveSystem : EntitySystem
{
    [ValidatePrototypeId<AntagPrototype>] private const string FugitiveAntagRole = "Fugitive";
    [ValidatePrototypeId<JobPrototype>] private const string FugitiveRole = "Fugitive";

    [ValidatePrototypeId<EntityPrototype>]
    private const string EscapeObjective = "EscapeShuttleObjectiveFugitive";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly FugitiveSystem _fugitiveSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly RandomHelperSystem _randomHelper = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawn,
            before: new[] { typeof(ArrivalsSystem), typeof(SpawnPointSystem) });
    }

    [ValidatePrototypeId<JobPrototype>]
    private const string JobSAI = "SAI";

    private void OnPlayerSpawn(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        if (!(args.Job?.Prototype != null &&
              _prototypeManager.TryIndex<JobPrototype>(args.Job!.Prototype!, out var jobInfo)))
        {
            return;
        }

        var possiblePositions = new List<EntityCoordinates>();
        {
            var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();


            while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                    continue;

                if (spawnPoint.SpawnType == SpawnPointType.Job &&
                    (args.Job == null || spawnPoint.Job?.ID == args.Job.Prototype))
                {
                    possiblePositions.Add(xform.Coordinates);
                }
            }
        }

        #region SAI

        if (possiblePositions.Count == 0 && args.Job?.Prototype == JobSAI)
        {
            var points = EntityQueryEnumerator<TelecomServerComponent, TransformComponent, MetaDataComponent>();

            while (points.MoveNext(out var uid, out _, out var xform, out var spawnPoint))
            {
                if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                    continue;

                possiblePositions.Add(
                    xform.Coordinates.WithPosition(xform.LocalPosition + xform.LocalRotation.ToWorldVec() * 1f));
            }
        }

        #endregion

        if (possiblePositions.Count == 0)
        {
            Log.Warning("No spawn points were available! MakeFugitive");

            _fugitiveSystem.MakeFugitive(out args.SpawnResult, true);
            return;
        }

        var spawnLoc = _random.Pick(possiblePositions);

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);
    }

    public bool MakeFugitive([NotNullWhen(true)] out EntityUid? Fugitive, bool forceHuman = false)
    {
        Fugitive = null;

        EntityUid? station = null;

        station ??= _stationSystem.GetStations().FirstOrNull(HasComp<StationEventEligibleComponent>);

        if (station == null || !TryComp<StationDataComponent>(station, out var stationDataComponent))
        {
            return false;
        }

        var spawnGrid = stationDataComponent.Grids.FirstOrNull(HasComp<BecomesStationComponent>);
        if (spawnGrid == null)
        {
            return false;
        }

        var latejoin = (from s in EntityQuery<SpawnPointComponent, TransformComponent>()
            where s.Item1.SpawnType == SpawnPointType.LateJoin && s.Item2.GridUid == spawnGrid
            select s.Item2.Coordinates).ToList();

        if (latejoin.Count == 0)
        {
            return false;
        }

        var coords = _random.Pick(latejoin);
        Fugitive = Spawn(forceHuman ? SpawnMobPrototype : SpawnPointPrototype, coords);

        return true;
    }

    private FormattedMessage GenerateFugiReport(EntityUid uid)
    {
        FormattedMessage report = new();
        report.AddMarkup(Loc.GetString("fugi-report-title", ("name", uid)));
        report.PushNewline();
        report.PushNewline();
        report.AddMarkup(Loc.GetString("fugitive-report-first-line", ("name", uid)));
        report.PushNewline();
        report.PushNewline();


        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoidComponent) ||
            !_prototypeManager.TryIndex<SpeciesPrototype>(humanoidComponent.Species, out var species))
        {
            report.AddMarkup(Loc.GetString("fugitive-report-inhuman", ("name", uid)));
            return report;
        }

        report.AddMarkup(Loc.GetString("fugitive-report-morphotype", ("species", Loc.GetString(species.Name))));
        report.PushNewline();
        report.AddMarkup(Loc.GetString("fugitive-report-age", ("age", humanoidComponent.Age)));
        report.PushNewline();

        var sexLine = string.Empty;
        sexLine += humanoidComponent.Sex switch
        {
            Sex.Male => Loc.GetString("fugitive-report-sex-m"),
            Sex.Female => Loc.GetString("fugitive-report-sex-f"),
            _ => Loc.GetString("fugitive-report-sex-n")
        };

        report.AddMarkup(sexLine);

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            report.PushNewline();
            report.AddMarkup(Loc.GetString("fugitive-report-weight", ("weight", Math.Round(physics.FixturesMass))));
        }

        report.PushNewline();
        report.PushNewline();
        report.AddMarkup(Loc.GetString("fugitive-report-last-line"));

        return report;
    }

    [ValidatePrototypeId<EntityPrototype>] private const string SpawnPointPrototype = "SpawnPointGhostFugitive";
    [ValidatePrototypeId<EntityPrototype>] private const string SpawnMobPrototype = "MobHumanFugitive";
}

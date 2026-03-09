using Content.Server.CrewManifest;
using Content.Server.DeviceLinking.Components;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Pinpointer;
using Content.Shared.CrewManifest;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Roles;
using Content.Shared.SecApartment;
using Content.Shared.Security.Components;
using Content.Shared.Station;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;

namespace Content.Server.Corvax.SecApartment;

public sealed partial class SecApartmentSystem : EntitySystem
{
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly CrewManifestSystem _crewManifest = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, StationData> _stationData = new();
    private readonly Dictionary<NetEntity, TimeSpan> _finishedTimers = new();

    private const string SecurityDepartment = "Security";
    private readonly HashSet<string> _securityJobs = new();
    private TimeSpan _lastSensorUpdate = TimeSpan.Zero;
    private TimeSpan _lastTimerUpdate = TimeSpan.Zero;

    private const int MaxSquadNameLength = 16;
    private const int MaxSquadDescriptionLength = 256;

    public override void Initialize()
    {
        base.Initialize();

        InitializeSecurityJobs();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);

        SubscribeLocalEvent<SecApartmentComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SecApartmentComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<SecApartmentComponent, BoundUIOpenedEvent>(OnUIOpened);

        SubscribeLocalEvent<SecApartmentComponent, CreateSquadMessage>(OnCreateSquad);
        SubscribeLocalEvent<SecApartmentComponent, DeleteSquadMessage>(OnDeleteSquad);
        SubscribeLocalEvent<SecApartmentComponent, RenameSquadMessage>(OnRenameSquad);
        SubscribeLocalEvent<SecApartmentComponent, ChangeSquadIconMessage>(OnChangeSquadIcon);
        SubscribeLocalEvent<SecApartmentComponent, UpdateSquadDescriptionMessage>(OnUpdateSquadDescription);
        SubscribeLocalEvent<SecApartmentComponent, AddMemberToSquadMessage>(OnAddMemberToSquad);
        SubscribeLocalEvent<SecApartmentComponent, RemoveMemberFromSquadMessage>(OnRemoveMemberFromSquad);
        SubscribeLocalEvent<SecApartmentComponent, ChangeSquadStatusMessage>(OnChangeSquadStatus);
        SubscribeLocalEvent<SecApartmentComponent, RemoveTimerMessage>(OnRemoveTimer);

        // TODO: I'm too lazy to change this.
        SubscribeLocalEvent<ActiveSignalTimerComponent, ComponentStartup>(OnTimerStartup);
        SubscribeLocalEvent<SignalTimerComponent, ComponentShutdown>(OnTimerComponentShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _gameTiming.CurTime;
        if (currentTime - _lastSensorUpdate >= TimeSpan.FromSeconds(5))
        {
            _lastSensorUpdate = currentTime;

            var query = EntityQueryEnumerator<SecApartmentComponent>();
            while (query.MoveNext(out var uid, out var comp))
                UpdateSensorStatuses(uid, comp);
        }

        if (currentTime - _lastTimerUpdate >= TimeSpan.FromSeconds(1))
        {
            _lastTimerUpdate = currentTime;
            UpdateAllTimerStates();
        }
    }

    private void InitializeSecurityJobs()
    {
        _securityJobs.Clear();
        foreach (var department in _prototype.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.ID == SecurityDepartment)
            {
                foreach (var role in department.Roles)
                    _securityJobs.Add(role);

                break;
            }
        }
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<JobPrototype>())
            InitializeSecurityJobs();

        if (obj.WasModified<DepartmentPrototype>())
            InitializeSecurityJobs();
    }

    private void UpdateSensorStatuses(EntityUid uid, SecApartmentComponent comp)
    {
        if (comp.Station == null)
            return;

        var securityCrew = GetSecurityCrew(uid, comp.Station.Value);
        var statusDict = new Dictionary<string, SuitSensorStatus?>();
        var squadLocations = new Dictionary<string, (string Location, bool HasLocation)>();

        var squads = _stationData.TryGetValue(comp.Station.Value, out var stationData)
            ? stationData.Squads : new List<Squad>();

        foreach (var squad in squads)
        {
            UpdateAndCollectSquadData(squad, securityCrew, statusDict);

            var location = GetSquadApproximateLocation(squad, securityCrew);
            squadLocations[squad.SquadId] = location;
        }

        if (!_ui.IsUiOpen(uid, SecApartmentUiKey.Key))
            return;

        var statusUpdate = new SensorStatusUpdateState(statusDict, squadLocations);
        _ui.SetUiState(uid, SecApartmentUiKey.Key, statusUpdate);
    }

    private void UpdateAndCollectSquadData(Squad squad, List<CrewMemberInfo> securityCrew,
        Dictionary<string, SuitSensorStatus?> statusDict)
    {
        foreach (var squadMember in squad.Members)
        {
            var currentMember = securityCrew.FirstOrDefault(c => c.MemberId == squadMember.MemberId);
            if (currentMember == null)
                continue;

            statusDict[squadMember.MemberId] = currentMember.SensorStatus;
            if (currentMember.OwnerUid != squadMember.OwnerUid)
            {
                if (squadMember.OwnerUid != null)
                {
                    var oldEntityUid = GetEntity(squadMember.OwnerUid.Value);
                    if (Exists(oldEntityUid) && !Terminating(oldEntityUid))
                        RemComp<SquadMemberComponent>(oldEntityUid);
                }

                squadMember.OwnerUid = currentMember.OwnerUid;

                if (squadMember.OwnerUid != null)
                {
                    var entityUid = GetEntity(squadMember.OwnerUid.Value);
                    if (Exists(entityUid) && !Terminating(entityUid))
                    {
                        EnsureComp<SquadMemberComponent>(entityUid, out var comp);
                        comp.StatusIcon = GetIconPrototypeId(squad.IconId);
                        Dirty(entityUid, comp);
                    }
                }
            }
        }
    }

    private void OnMapInit(Entity<SecApartmentComponent> ent, ref MapInitEvent args)
    {
        var station = _station.GetStationInMap(Transform(ent).MapID);
        ent.Comp.Station = station;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnUIOpenAttempt(Entity<SecApartmentComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (ent.Comp.Station == null)
            args.Cancel();
    }

    private void OnUIOpened(Entity<SecApartmentComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    #region UI Message Handlers

    private void OnCreateSquad(EntityUid uid, SecApartmentComponent component, CreateSquadMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.SquadName) || component.Station == null)
            return;

        if (!_stationData.TryGetValue(component.Station.Value, out var stationData))
        {
            stationData = new StationData();
            _stationData[component.Station.Value] = stationData;
        }

        var squadId = $"squad_{_random.Next(1000, 9999)}";
        var squadName = SanitizeString(msg.SquadName, MaxSquadNameLength);
        var squad = new Squad(squadId, squadName);
        stationData.Squads.Add(squad);

        UpdateAllTabletsOnStation(component.Station.Value);
    }

    private void OnDeleteSquad(EntityUid uid, SecApartmentComponent component, DeleteSquadMessage msg)
    {
        if (component.Station == null || !_stationData.TryGetValue(component.Station.Value, out var stationData))
            return;

        var squad = stationData.Squads.FirstOrDefault(s => s.SquadId == msg.SquadId);
        if (squad == null)
            return;

        foreach (var member in squad.Members)
        {
            RemoveSquadMemberComponent(member);
        }

        stationData.Squads.Remove(squad);
        UpdateAllTabletsOnStation(component.Station.Value);
    }

    private void OnRenameSquad(EntityUid uid, SecApartmentComponent component, RenameSquadMessage msg)
    {
        if (component.Station == null || !_stationData.TryGetValue(component.Station.Value, out var stationData)
            || string.IsNullOrWhiteSpace(msg.NewName))
            return;

        var squad = stationData.Squads.FirstOrDefault(s => s.SquadId == msg.SquadId);
        if (squad == null)
            return;

        squad.Name = SanitizeString(msg.NewName, MaxSquadNameLength);
        UpdateAllTabletsOnStation(component.Station.Value);
    }

    private void OnChangeSquadIcon(EntityUid uid, SecApartmentComponent component, ChangeSquadIconMessage msg)
    {
        if (component.Station == null || !_stationData.TryGetValue(component.Station.Value, out var stationData))
            return;

        var squad = stationData.Squads.FirstOrDefault(s => s.SquadId == msg.SquadId);
        if (squad == null)
            return;

        squad.IconId = msg.IconId;
        UpdateAllTabletsOnStation(component.Station.Value);

        UpdateSquadMemberIcons(squad, uid, component.Station.Value);
    }

    private void OnUpdateSquadDescription(EntityUid uid, SecApartmentComponent component, UpdateSquadDescriptionMessage msg)
    {
        if (component.Station == null || !_stationData.TryGetValue(component.Station.Value, out var stationData))
            return;

        var squad = stationData.Squads.FirstOrDefault(s => s.SquadId == msg.SquadId);
        if (squad == null)
            return;

        squad.Description = SanitizeString(msg.Description, MaxSquadDescriptionLength);
        UpdateAllTabletsOnStation(component.Station.Value);
    }

    private void OnAddMemberToSquad(EntityUid uid, SecApartmentComponent component, AddMemberToSquadMessage msg)
    {
        if (component.Station == null || !_stationData.TryGetValue(component.Station.Value, out var stationData))
            return;

        var squad = stationData.Squads.FirstOrDefault(s => s.SquadId == msg.SquadId);
        if (squad == null)
            return;

        var securityCrew = GetSecurityCrew(uid, component.Station.Value);
        var member = securityCrew.FirstOrDefault(c => c.MemberId == msg.MemberId);
        if (member == null)
            return;

        foreach (var otherSquad in stationData.Squads)
        {
            if (otherSquad.Members.RemoveAll(m => m.MemberId == msg.MemberId) > 0)
                RemoveSquadMemberComponent(member);
        }

        if (!squad.Members.Any(m => m.MemberId == msg.MemberId))
        {
            squad.Members.Add(member);
            AddSquadMemberComponent(member, GetIconPrototypeId(squad.IconId));
        }

        UpdateAllTabletsOnStation(component.Station.Value);
    }

    private void OnRemoveMemberFromSquad(EntityUid uid, SecApartmentComponent component, RemoveMemberFromSquadMessage msg)
    {
        if (component.Station == null || !_stationData.TryGetValue(component.Station.Value, out var stationData))
            return;

        var squad = stationData.Squads.FirstOrDefault(s => s.SquadId == msg.SquadId);
        if (squad == null)
            return;

        var member = squad.Members.FirstOrDefault(m => m.MemberId == msg.MemberId);
        squad.Members.RemoveAll(m => m.MemberId == msg.MemberId);

        if (member != null)
        {
            RemoveSquadMemberComponent(member);
        }

        UpdateAllTabletsOnStation(component.Station.Value);
    }

    private void OnChangeSquadStatus(EntityUid uid, SecApartmentComponent component, ChangeSquadStatusMessage msg)
    {
        if (component.Station == null || !_stationData.TryGetValue(component.Station.Value, out var stationData))
            return;

        var squad = stationData.Squads.FirstOrDefault(s => s.SquadId == msg.SquadId);
        if (squad == null)
            return;

        squad.Status = msg.Status;
        UpdateAllTabletsOnStation(component.Station.Value);
    }

    private void OnRemoveTimer(EntityUid uid, SecApartmentComponent component, RemoveTimerMessage msg)
    {
        var timerUid = GetEntity(msg.TimerUid);
        if (Exists(timerUid) && HasComp<SignalTimerComponent>(timerUid))
            RemoveTimerFromTrack(timerUid);
    }

    #endregion

    private void UpdateSquadMemberIcons(Squad squad, EntityUid tabletUid, EntityUid station)
    {
        var securityCrew = GetSecurityCrew(tabletUid, station);

        foreach (var squadMember in squad.Members)
        {
            var currentMember = securityCrew.FirstOrDefault(c => c.MemberId == squadMember.MemberId);
            if (currentMember != null)
            {
                squadMember.OwnerUid = currentMember.OwnerUid;
                AddSquadMemberComponent(squadMember, GetIconPrototypeId(squad.IconId));
            }
        }
    }

    private void AddSquadMemberComponent(CrewMemberInfo member, string iconId)
    {
        if (member.OwnerUid == null)
            return;

        var entityUid = GetEntity(member.OwnerUid.Value);
        if (!Exists(entityUid) || Terminating(entityUid))
            return;

        EnsureComp<SquadMemberComponent>(entityUid, out var comp);
        comp.StatusIcon = iconId;
        Dirty(entityUid, comp);
    }

    private void RemoveSquadMemberComponent(CrewMemberInfo member)
    {
        if (member.OwnerUid == null)
            return;

        var entityUid = GetEntity(member.OwnerUid.Value);
        if (!Exists(entityUid) || Terminating(entityUid))
            return;

        RemComp<SquadMemberComponent>(entityUid);
    }

    private void UpdateUi(EntityUid uid, SecApartmentComponent comp)
    {
        if (!_ui.HasUi(uid, SecApartmentUiKey.Key) || comp.Station == null)
            return;

        var stationName = MetaData(comp.Station.Value).EntityName;
        var securityCrew = GetSecurityCrew(uid, comp.Station.Value);

        var squads = _stationData.TryGetValue(comp.Station.Value, out var stationData)
            ? stationData.Squads : new List<Squad>();

        var assignedMemberIds = new HashSet<string>();
        foreach (var squad in squads)
        {
            foreach (var member in squad.Members)
            {
                assignedMemberIds.Add(member.MemberId);
            }
        }

        var unassignedSecurity = securityCrew
            .Where(member => !assignedMemberIds.Contains(member.MemberId))
            .ToList();

        var state = new SecApartmentUpdateState(
            stationName,
            securityCrew,
            unassignedSecurity,
            squads
        );

        _ui.SetUiState(uid, SecApartmentUiKey.Key, state);
    }

    private void UpdateAllTabletsOnStation(EntityUid station)
    {
        var query = EntityQueryEnumerator<SecApartmentComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Station == station)
                UpdateUi(uid, comp);
        }
    }

    private List<CrewMemberInfo> GetSecurityCrew(EntityUid tablet, EntityUid station)
    {
        var result = new List<CrewMemberInfo>();

        var (_, manifest) = _crewManifest.GetCrewManifest(station);
        if (manifest == null)
            return result;

        foreach (var entry in manifest.Entries)
        {
            if (_securityJobs.Contains(entry.JobPrototype))
            {
                NetEntity? ownerUid = null;
                SuitSensorStatus? status = null;
                if (TryComp<CrewMonitoringConsoleComponent>(tablet, out var monitoring))
                {
                    var sensor = monitoring.ConnectedSensors.Values
                        .FirstOrDefault(s => s.Name == entry.Name && s.Job == entry.JobTitle);

                    status = sensor;
                    ownerUid = sensor?.OwnerUid;
                }

                var memberId = GenerateMemberId(entry);
                result.Add(new CrewMemberInfo(
                    memberId,
                    ownerUid,
                    entry.Name,
                    entry.JobTitle,
                    entry.JobIcon,
                    status
                ));
            }
        }

        return result;
    }

    private string GenerateMemberId(CrewManifestEntry entry)
    {
        return $"{entry.Name.GetHashCode():X8}_{entry.JobPrototype}_{entry.JobTitle.GetHashCode():X8}";
    }

    private (string Location, bool HasLocation) GetSquadApproximateLocation(Squad squad, List<CrewMemberInfo> securityCrew)
    {
        var trackedPositions = new List<Vector2>();
        var mapId = MapId.Nullspace;

        foreach (var memberId in squad.Members.Select(m => m.MemberId))
        {
            var memberInfo = securityCrew.FirstOrDefault(c => c.MemberId == memberId);
            if (memberInfo?.SensorStatus == null)
                continue;

            if (!memberInfo.SensorStatus.IsAlive)
                continue;

            var ownerUid = GetEntity(memberInfo.SensorStatus.OwnerUid);
            var memberTransform = Transform(ownerUid);
            if (memberTransform.GridUid == null)
                continue;

            var mapPos = _transform.GetMapCoordinates(ownerUid);

            trackedPositions.Add(mapPos.Position);
            mapId = mapPos.MapId;
        }

        if (trackedPositions.Count == 0)
            return (Loc.GetString("sec-apartment-unknown"), false);

        var averagePos = Vector2.Zero;
        foreach (var pos in trackedPositions)
        {
            averagePos += pos;
        }
        averagePos /= trackedPositions.Count;

        try
        {
            var mapCoords = new MapCoordinates(averagePos, mapId);
            var locationText = _navMap.GetNearestBeaconString(mapCoords, onlyName: true);
            return (locationText, true);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to get squad location: {ex}");
            return (Loc.GetString("sec-apartment-unknown"), false);
        }
    }

    private string GetIconPrototypeId(SquadIconNum icon)
    {
        return icon switch
        {
            SquadIconNum.Alpha => "SecuritySquadIconAlpha",
            SquadIconNum.Beta => "SecuritySquadIconBeta",
            SquadIconNum.Gamma => "SecuritySquadIconGamma",
            SquadIconNum.Delta => "SecuritySquadIconDelta",
            SquadIconNum.Epsilon => "SecuritySquadIconEpsilon",
            SquadIconNum.Zeta => "SecuritySquadIconZeta",
            SquadIconNum.Heta => "SecuritySquadIconHeta",
            SquadIconNum.Theta => "SecuritySquadIconTheta",
            SquadIconNum.Iota => "SecuritySquadIconIota",
            SquadIconNum.Kappa => "SecuritySquadIconKappa",
            SquadIconNum.Lambda => "SecuritySquadIconLambda",
            SquadIconNum.Mu => "SecuritySquadIconMu",
            SquadIconNum.Nu => "SecuritySquadIconNu",
            SquadIconNum.Xi => "SecuritySquadIconXi",
            SquadIconNum.Omicron => "SecuritySquadIconOmicron",
            SquadIconNum.Pi => "SecuritySquadIconPi",
            SquadIconNum.Ro => "SecuritySquadIconRo",
            SquadIconNum.Sigma => "SecuritySquadIconSigma",
            SquadIconNum.Tau => "SecuritySquadIconTau",
            SquadIconNum.Upsilon => "SecuritySquadIconUpsilon",
            SquadIconNum.Fi => "SecuritySquadIconFi",
            SquadIconNum.Hi => "SecuritySquadIconHi",
            SquadIconNum.Psi => "SecuritySquadIconPsi",
            SquadIconNum.Omega => "SecuritySquadIconOmega",
            _ => "SecuritySquadIconAlpha"
        };
    }

    private static string SanitizeString(string input, int maxLength)
    {
        var sanitized = FormattedMessage.RemoveMarkupPermissive(input);
        if (sanitized.Length > maxLength)
            sanitized = sanitized[..maxLength];
        return sanitized;
    }
    #region Timers
    private void OnTimerStartup(EntityUid uid, ActiveSignalTimerComponent component, ComponentStartup args)
    {
        AddTimerToTrack(uid);
    }

    private void OnTimerComponentShutdown(EntityUid uid, SignalTimerComponent component, ComponentShutdown args)
    {
        RemoveTimerFromTrack(uid);
    }

    private void AddTimerToTrack(EntityUid timerUid)
    {
        var station = _station.GetStationInMap(Transform(timerUid).MapID);
        if (station == null)
            return;

        if (!_stationData.TryGetValue(station.Value, out var stationData))
        {
            stationData = new StationData();
            _stationData[station.Value] = stationData;
        }

        var netUid = GetNetEntity(timerUid);

        _finishedTimers.Remove(netUid);
        stationData.TrackedTimers.Add(netUid);
        UpdateTimerStateForStation(station.Value);
    }

    private void RemoveTimerFromTrack(EntityUid timerUid)
    {
        var netEntity = GetNetEntity(timerUid);
        foreach (var stationData in _stationData.Values)
        {
            if (stationData.TrackedTimers.Remove(netEntity))
            {
                _finishedTimers.Remove(netEntity);

                var station = _stationData.FirstOrDefault(x => x.Value == stationData).Key;
                UpdateTimerStateForStation(station);
                break;
            }
        }
    }

    private void UpdateAllTimerStates()
    {
        var stationsToUpdate = new HashSet<EntityUid>();
        foreach (var (station, stationData) in _stationData)
        {
            if (stationData.TrackedTimers.Count > 0)
                stationsToUpdate.Add(station);
        }

        foreach (var station in stationsToUpdate)
            UpdateTimerStateForStation(station);
    }

    private void UpdateTimerStateForStation(EntityUid station)
    {
        var timers = new List<TimerEntry>();

        if (_stationData.TryGetValue(station, out var stationData))
        {
            foreach (var netEntity in stationData.TrackedTimers.ToList())
            {
                var timerUid = GetEntity(netEntity);
                if (!Exists(timerUid))
                {
                    stationData.TrackedTimers.Remove(netEntity);
                    continue;
                }

                if (TryComp<SignalTimerComponent>(timerUid, out var timerComp))
                {
                    TimeSpan remaining;
                    var total = TimeSpan.FromSeconds(timerComp.Delay);
                    if (TryComp<ActiveSignalTimerComponent>(timerUid, out var activeComp))
                    {
                        remaining = activeComp.TriggerTime - _gameTiming.CurTime;
                    }
                    else
                    {
                        if (!_finishedTimers.TryGetValue(netEntity, out var finishedTime))
                        {
                            finishedTime = _gameTiming.CurTime;
                            _finishedTimers[netEntity] = finishedTime;
                        }
                        remaining = finishedTime - _gameTiming.CurTime;
                    }

                    timers.Add(new TimerEntry(netEntity, timerComp.Label, remaining, total));
                }
                else
                {
                    stationData.TrackedTimers.Remove(netEntity);
                }
            }
        }

        var query = EntityQueryEnumerator<SecApartmentComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Station == station && _ui.IsUiOpen(uid, SecApartmentUiKey.Key))
            {
                var state = new TimerUpdateState(timers);
                _ui.SetUiState(uid, SecApartmentUiKey.Key, state);
            }
        }
    }
    #endregion
}

public sealed class StationData
{
    public List<Squad> Squads { get; } = new();
    public readonly HashSet<NetEntity> TrackedTimers = new();
}

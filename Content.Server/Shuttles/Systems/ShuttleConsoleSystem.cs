using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Popups;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Tag;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Shuttles.UI.MapObjects;
using Content.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Content.Shared.Projectiles; // #SB AndreyCamper
using Robust.Shared.Maths; // #SB AndreyCamper
using Content.Shared.Weapons.Hitscan.Components; // #SB AndreyCamper

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem : SharedShuttleConsoleSystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContentEyeSystem _eyeSystem = default!;

    private static readonly ProtoId<TagPrototype> RadarVisibleTag = "RadarVisible"; // #SB AndreyCamper
    private static readonly ProtoId<TagPrototype> RadarVisibleSmallTag = "RadarVisibleSmall"; // #SB AndreyCamper
    private static readonly ProtoId<TagPrototype> RadarVisibleRectTag = "RadarVisibleRect"; // #SB AndreyCamper
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly HashSet<Entity<ShuttleConsoleComponent>> _consoles = new();

    private readonly HashSet<EntityUid> _activeRadarConsoles = new(); // #SB AndreyCamper Список для оптимизации (чтобы помнить, кому слать очистку экрана)

    private static readonly ProtoId<TagPrototype> CanPilotTag = "CanPilot";

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ShuttleConsoleComponent, PowerChangedEvent>(OnConsolePowerChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, AnchorStateChangedEvent>(OnConsoleAnchorChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, AfterActivatableUIOpenEvent>(OnConsoleUIOpenAttempt);
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<ShuttleConsoleFTLBeaconMessage>(OnBeaconFTLMessage);
            subs.Event<ShuttleConsoleFTLPositionMessage>(OnPositionFTLMessage);
            subs.Event<BoundUIClosedEvent>(OnConsoleUIClose);
        });

        SubscribeLocalEvent<DroneConsoleComponent, ConsoleShuttleEvent>(OnCargoGetConsole);
        SubscribeLocalEvent<DroneConsoleComponent, AfterActivatableUIOpenEvent>(OnDronePilotConsoleOpen);
        Subs.BuiEvents<DroneConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnDronePilotConsoleClose);
        });

        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);

        SubscribeLocalEvent<PilotComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<PilotComponent, StopPilotingAlertEvent>(OnStopPilotingAlert);

        SubscribeLocalEvent<FTLDestinationComponent, ComponentStartup>(OnFtlDestStartup);
        SubscribeLocalEvent<FTLDestinationComponent, ComponentShutdown>(OnFtlDestShutdown);

        InitializeFTL();
    }

    private void OnFtlDestStartup(EntityUid uid, FTLDestinationComponent component, ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnFtlDestShutdown(EntityUid uid, FTLDestinationComponent component, ComponentShutdown args)
    {
        RefreshShuttleConsoles();
    }

    private void OnDock(DockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    private void OnUndock(UndockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    /// <summary>
    /// Refreshes all the shuttle console data for a particular grid.
    /// </summary>
    public void RefreshShuttleConsoles(EntityUid gridUid)
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        _consoles.Clear();
        _lookup.GetChildEntities(gridUid, _consoles);
        DockingInterfaceState? dockState = null;

        foreach (var entity in _consoles)
        {
            UpdateState(entity, ref dockState);
        }
    }

    /// <summary>
    /// Refreshes all of the data for shuttle consoles.
    /// </summary>
    public void RefreshShuttleConsoles()
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        var query = AllEntityQuery<ShuttleConsoleComponent>();
        DockingInterfaceState? dockState = null;

        while (query.MoveNext(out var uid, out _))
        {
            UpdateState(uid, ref dockState);
        }
    }

    /// <summary>
    /// Stop piloting if the window is closed.
    /// </summary>
    private void OnConsoleUIClose(EntityUid uid, ShuttleConsoleComponent component, BoundUIClosedEvent args)
    {
        if ((ShuttleConsoleUiKey)args.UiKey != ShuttleConsoleUiKey.Key)
        {
            return;
        }

        RemovePilot(args.Actor);
    }

    private void OnConsoleUIOpenAttempt(EntityUid uid, ShuttleConsoleComponent component,
        AfterActivatableUIOpenEvent args)
    {
        TryPilot(args.User, uid);
    }

    private void OnConsoleAnchorChange(EntityUid uid, ShuttleConsoleComponent component,
        ref AnchorStateChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private void OnConsolePowerChange(EntityUid uid, ShuttleConsoleComponent component, ref PowerChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private bool TryPilot(EntityUid user, EntityUid uid)
    {
        if (!_tags.HasTag(user, CanPilotTag) ||
            !TryComp<ShuttleConsoleComponent>(uid, out var component) ||
            !this.IsPowered(uid, EntityManager) ||
            !Transform(uid).Anchored ||
            !_blocker.CanInteract(user, uid))
        {
            return false;
        }

        var pilotComponent = EnsureComp<PilotComponent>(user);
        var console = pilotComponent.Console;

        if (console != null)
        {
            RemovePilot(user, pilotComponent);

            // This feels backwards; is this intended to be a toggle?
            if (console == uid)
                return false;
        }

        AddPilot(uid, user, component);
        return true;
    }

    private void OnGetState(EntityUid uid, PilotComponent component, ref ComponentGetState args)
    {
        args.State = new PilotComponentState(GetNetEntity(component.Console));
    }

    private void OnStopPilotingAlert(Entity<PilotComponent> ent, ref StopPilotingAlertEvent args)
    {
        if (ent.Comp.Console != null)
        {
            RemovePilot(ent, ent);
        }
    }

    /// <summary>
    /// Returns the position and angle of all dockingcomponents.
    /// </summary>
    public Dictionary<NetEntity, List<DockingPortState>> GetAllDocks()
    {
        // TODO: NEED TO MAKE SURE THIS UPDATES ON ANCHORING CHANGES!
        var result = new Dictionary<NetEntity, List<DockingPortState>>();
        var query = AllEntityQuery<DockingComponent, TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform, out var metadata))
        {
            if (xform.ParentUid != xform.GridUid)
                continue;

            var gridDocks = result.GetOrNew(GetNetEntity(xform.GridUid.Value));

            var state = new DockingPortState()
            {
                Name = metadata.EntityName,
                Coordinates = GetNetCoordinates(xform.Coordinates),
                Angle = xform.LocalRotation,
                Entity = GetNetEntity(uid),
                GridDockedWith =
                    _xformQuery.TryGetComponent(comp.DockedWith, out var otherDockXform) ?
                    GetNetEntity(otherDockXform.GridUid) :
                    null,
                Color = comp.RadarColor,
                HighlightedColor = comp.HighlightedRadarColor
            };

            gridDocks.Add(state);
        }

        return result;
    }

    private void UpdateState(EntityUid consoleUid, ref DockingInterfaceState? dockState)
    {
        EntityUid? entity = consoleUid;

        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = entity,
        };

        RaiseLocalEvent(entity.Value, ref getShuttleEv);
        entity = getShuttleEv.Console;

        TryComp(entity, out TransformComponent? consoleXform);
        var shuttleGridUid = consoleXform?.GridUid;

        NavInterfaceState navState;
        ShuttleMapInterfaceState mapState;
        dockState ??= GetDockState();

        if (shuttleGridUid != null && entity != null)
        {
            navState = GetNavState(entity.Value, dockState.Docks);
            mapState = GetMapState(shuttleGridUid.Value);
        }
        else
        {
            // #SB AndreyCamper ИСПРАВЛЕНИЕ: Добавлен список лазеров в конец конструктора
            navState = new NavInterfaceState(
                0f,
                null,
                null,
                new Dictionary<NetEntity, List<DockingPortState>>(),
                new List<(NetCoordinates, Angle, byte)>(),
                new List<(NetCoordinates, Angle, float, byte)>()); // Лазеры

            mapState = new ShuttleMapInterfaceState(
                FTLState.Invalid,
                default,
                new List<ShuttleBeaconObject>(),
                new List<ShuttleExclusionObject>());
        }

        if (_ui.HasUi(consoleUid, ShuttleConsoleUiKey.Key))
        {
            _ui.SetUiState(consoleUid, ShuttleConsoleUiKey.Key, new ShuttleBoundUserInterfaceState(navState, mapState, dockState));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toRemove = new ValueList<(EntityUid, PilotComponent)>();
        var query = EntityQueryEnumerator<PilotComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Console == null)
                continue;

            if (!_blocker.CanInteract(uid, comp.Console))
            {
                toRemove.Add((uid, comp));
            }
        }

        foreach (var (uid, comp) in toRemove)
        {
            RemovePilot(uid, comp);
        }

        var consoleQuery = EntityQueryEnumerator<ShuttleConsoleComponent, RadarConsoleComponent, TransformComponent>();

        while (consoleQuery.MoveNext(out var uid, out var shuttleComp, out var radar, out var xform))
        {
            // Если интерфейс закрыт, ничего не делаем
            if (!_ui.IsUiOpen(uid, ShuttleConsoleUiKey.Key))
            {
                _activeRadarConsoles.Remove(uid);
                continue;
            }

            // 1. Ищем пули
            var projectiles = new List<(NetCoordinates, Angle, byte)>();

            var mapId = xform.MapID;
            var centerPos = _transform.GetWorldPosition(xform);
            var rangeSquared = radar.MaxRange * radar.MaxRange;

            var projQuery = EntityManager.EntityQueryEnumerator<ProjectileComponent, MetaDataComponent, TransformComponent>();
            while (projQuery.MoveNext(out var projUid, out _, out var meta, out var projXform))
            {
                // Проверяем тег
                byte type = 0;
                if (_tags.HasTag(projUid, RadarVisibleRectTag))
                {
                    type = 2; // Rect
                }
                else if (_tags.HasTag(projUid, RadarVisibleSmallTag))
                {
                    type = 1; // Small
                }
                else if (_tags.HasTag(projUid, RadarVisibleTag))
                {
                    type = 0; // Default
                }
                else
                {
                    continue; // Skip
                }

                if (projXform.MapID != mapId)
                    continue;
                if ((_transform.GetWorldPosition(projXform) - centerPos).LengthSquared() > rangeSquared)
                    continue;

                // Добавляем (Координаты, Угол)
                projectiles.Add((GetNetCoordinates(projXform.Coordinates), projXform.LocalRotation, type));
            }

            // 2. LASERS (Новый подход через Компонент)
            var lasers = new List<(NetCoordinates, Angle, float, byte)>();

            // Ищем сущности, у которых есть наш компонент данных
            var laserQuery = EntityManager.EntityQueryEnumerator<RadarLaserVisualsComponent, TransformComponent>();

            while (laserQuery.MoveNext(out var _, out var visuals, out var lXform))
            {
                if (lXform.MapID != mapId) continue;
                if ((_transform.GetWorldPosition(lXform) - centerPos).LengthSquared() > rangeSquared) continue;

                // Читаем данные напрямую из компонента!
                // Никаких Transform.Scale и никаких Vector2.
                float length = visuals.Length;
                Angle angle = visuals.Angle;
                byte type = visuals.Type;

                lasers.Add((GetNetCoordinates(lXform.Coordinates), angle, length, type));
            }

            // 2. Отправляем сообщение
            if (projectiles.Count > 0 || lasers.Count > 0)
            {
                _ui.ServerSendUiMessage(uid, ShuttleConsoleUiKey.Key, new RadarProjectileMessage(projectiles, lasers));
                _activeRadarConsoles.Add(uid);
            }
            else if (_activeRadarConsoles.Contains(uid))
            {
                // Если снаряды исчезли, шлем пустой список 1 раз
                _ui.ServerSendUiMessage(uid, ShuttleConsoleUiKey.Key, new RadarProjectileMessage(projectiles, lasers));
                _activeRadarConsoles.Remove(uid);
            }
            // #SB AndreyCamper end
        }
    }

    protected override void HandlePilotShutdown(EntityUid uid, PilotComponent component, ComponentShutdown args)
    {
        base.HandlePilotShutdown(uid, component, args);
        RemovePilot(uid, component);
    }

    private void OnConsoleShutdown(EntityUid uid, ShuttleConsoleComponent component, ComponentShutdown args)
    {
        ClearPilots(component);
    }

    public void AddPilot(EntityUid uid, EntityUid entity, ShuttleConsoleComponent component)
    {
        if (!TryComp(entity, out PilotComponent? pilotComponent)
        || component.SubscribedPilots.Contains(entity))
        {
            return;
        }

        _eyeSystem.SetZoom(entity, component.Zoom, ignoreLimits: true);

        component.SubscribedPilots.Add(entity);

        _alertsSystem.ShowAlert(entity, pilotComponent.PilotingAlert);

        pilotComponent.Console = uid;
        ActionBlockerSystem.UpdateCanMove(entity);
        pilotComponent.Position = Comp<TransformComponent>(entity).Coordinates;
        Dirty(entity, pilotComponent);
    }

    public void RemovePilot(EntityUid pilotUid, PilotComponent pilotComponent)
    {
        var console = pilotComponent.Console;

        if (!TryComp<ShuttleConsoleComponent>(console, out var helm))
            return;

        pilotComponent.Console = null;
        pilotComponent.Position = null;
        _eyeSystem.ResetZoom(pilotUid);

        if (!helm.SubscribedPilots.Remove(pilotUid))
            return;

        _alertsSystem.ClearAlert(pilotUid, pilotComponent.PilotingAlert);

        _popup.PopupEntity(Loc.GetString("shuttle-pilot-end"), pilotUid, pilotUid);

        if (pilotComponent.LifeStage < ComponentLifeStage.Stopping)
            RemComp<PilotComponent>(pilotUid);
    }

    public void RemovePilot(EntityUid entity)
    {
        if (!TryComp(entity, out PilotComponent? pilotComponent))
            return;

        RemovePilot(entity, pilotComponent);
    }

    public void ClearPilots(ShuttleConsoleComponent component)
    {
        var query = GetEntityQuery<PilotComponent>();
        while (component.SubscribedPilots.TryGetValue(0, out var pilot))
        {
            if (query.TryGetComponent(pilot, out var pilotComponent))
                RemovePilot(pilot, pilotComponent);
        }
    }

    /// <summary>
    /// Specific for a particular shuttle.
    /// </summary>
    public NavInterfaceState GetNavState(Entity<RadarConsoleComponent?, TransformComponent?> entity, Dictionary<NetEntity, List<DockingPortState>> docks,
        List<(NetCoordinates, Angle, byte)>? projectiles = null, // #SB AndreyCamper
        List<(NetCoordinates, Angle, float, byte)>? lasers = null) // #SB AndreyCamper
    {
        if (projectiles == null) projectiles = new List<(NetCoordinates, Angle, byte)>(); // #SB AndreyCamper
        if (lasers == null) lasers = new List<(NetCoordinates, Angle, float, byte)>(); // #SB AndreyCamper
        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2))
            return new NavInterfaceState(SharedRadarConsoleSystem.DefaultMaxRange, null, null, docks, projectiles, lasers); // #SB AndreyCamper

        return GetNavState(
            entity,
            docks,
            entity.Comp2.Coordinates,
            entity.Comp2.LocalRotation, // #SB AndreyCamper
            projectiles, // #SB AndreyCamper
            lasers); // #SB AndreyCamper
    }

    public NavInterfaceState GetNavState(
        Entity<RadarConsoleComponent?, TransformComponent?> entity,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        EntityCoordinates coordinates,
        Angle angle,
        List<(NetCoordinates, Angle, byte)>? projectiles = null, // #SB AndreyCamper
        List<(NetCoordinates, Angle, float, byte)>? lasers = null) // #SB AndreyCamper
{
        // #SB AndreyCamper start Если список не передан извне (из RadarConsole), ищем сами
        if (projectiles == null) projectiles = new List<(NetCoordinates, Angle, byte)>();
        if (lasers == null) lasers = new List<(NetCoordinates, Angle, float, byte)>();
        {
            projectiles = new List<(NetCoordinates, Angle, byte)>();

            var mapId = _transform.GetMapId(coordinates);
            var centerPos = _transform.ToMapCoordinates(coordinates).Position;
            var range = entity.Comp1?.MaxRange ?? SharedRadarConsoleSystem.DefaultMaxRange;
            var rangeSquared = range * range;

            var query = EntityManager.EntityQueryEnumerator<ProjectileComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                // Используем проверку тега, как в Update
                byte type = 0;
                if (_tags.HasTag(uid, RadarVisibleRectTag))
                    type = 2; // Rect
                else if (_tags.HasTag(uid, RadarVisibleSmallTag))
                    type = 1; // Small
                else if (_tags.HasTag(uid, RadarVisibleTag))
                    type = 0; // Default
                else
                    continue;

                if (xform.MapID != mapId)
                    continue;
                if ((_transform.GetWorldPosition(xform) - centerPos).LengthSquared() > rangeSquared)
                    continue;

                projectiles.Add((GetNetCoordinates(xform.Coordinates), xform.LocalRotation, type));
            }
        }
        // #SB AndreyCamper end

        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2))
            return new NavInterfaceState(SharedRadarConsoleSystem.DefaultMaxRange, GetNetCoordinates(coordinates), angle, docks, projectiles, lasers);

        return new NavInterfaceState(
            entity.Comp1.MaxRange,
            GetNetCoordinates(coordinates),
            angle,
            docks, // #SB AndreyCamper
            projectiles, // #SB AndreyCamper
            lasers); // #SB AndreyCamper
    }

    /// <summary>
    /// Global for all shuttles.
    /// </summary>
    /// <returns></returns>
    public DockingInterfaceState GetDockState()
    {
        var docks = GetAllDocks();
        return new DockingInterfaceState(docks);
    }

    /// <summary>
    /// Specific to a particular shuttle.
    /// </summary>
    public ShuttleMapInterfaceState GetMapState(Entity<FTLComponent?> shuttle)
    {
        FTLState ftlState = FTLState.Available;
        StartEndTime stateDuration = default;

        if (Resolve(shuttle, ref shuttle.Comp, false) && shuttle.Comp.LifeStage < ComponentLifeStage.Stopped)
        {
            ftlState = shuttle.Comp.State;
            stateDuration = _shuttle.GetStateTime(shuttle.Comp);
        }

        List<ShuttleBeaconObject>? beacons = null;
        List<ShuttleExclusionObject>? exclusions = null;
        GetBeacons(ref beacons);
        GetExclusions(ref exclusions);

        return new ShuttleMapInterfaceState(
            ftlState,
            stateDuration,
            beacons ?? new List<ShuttleBeaconObject>(),
            exclusions ?? new List<ShuttleExclusionObject>());
    }
}

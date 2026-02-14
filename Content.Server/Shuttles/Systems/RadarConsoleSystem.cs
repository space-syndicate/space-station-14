using System.Numerics;
using Content.Server.UserInterface;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.PowerCell;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Shared.Projectiles; // #SB AndreyCamper
using Content.Shared.Tag;         // #SB AndreyCamper
using Robust.Shared.Prototypes;   // #SB AndreyCamper
using Content.Shared.Weapons.Hitscan.Components; // #SB AndreyCamper

namespace Content.Server.Shuttles.Systems;

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    // #SB AndreyCamper start
	[Dependency] private readonly TagSystem _tags = default!;
	[Dependency] private readonly SharedTransformSystem _transform = default!;
    private static readonly ProtoId<TagPrototype> RadarVisibleTag = "RadarVisible";
    private static readonly ProtoId<TagPrototype> RadarVisibleSmallTag = "RadarVisibleSmall";
    private static readonly ProtoId<TagPrototype> RadarVisibleRectTag = "RadarVisibleRect";
	private readonly HashSet<EntityUid> _activeRadarConsoles = new();
    // #SB AndreyCamper end
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);
    }

    private void OnRadarStartup(EntityUid uid, RadarConsoleComponent component, ComponentStartup args)
    {
        UpdateState(uid, component);
    }

    protected override void UpdateState(EntityUid uid, RadarConsoleComponent component)
    {
        var xform = Transform(uid);
        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (component.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (_uiSystem.HasUi(uid, RadarConsoleUiKey.Key))
        {
            NavInterfaceState state;
            var docks = _console.GetAllDocks();

			var emptyProjectiles = new List<(NetCoordinates, Angle, byte)>(); // #SB AndreyCamper Заглушка
            var emptyLasers = new List<(NetCoordinates, Angle, float, byte)>(); // #SB AndreyCamper

            if (coordinates != null && angle != null)
            {
                state = _console.GetNavState(uid, docks, coordinates.Value, angle.Value, emptyProjectiles, emptyLasers); // #SB AndreyCamper
            }
            else
            {
                state = _console.GetNavState(uid, docks);
            }

            state.RotateWithEntity = !component.FollowEntity;

            _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, new NavBoundUserInterfaceState(state));
        }
    }
    // #SB AndreyCamper start
	public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RadarConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var radar, out var xform))
        {
            // Если интерфейс закрыт — пропускаем
            if (!_uiSystem.IsUiOpen(uid, RadarConsoleUiKey.Key))
            {
                _activeRadarConsoles.Remove(uid);
                continue;
            }

            // 1. Ищем пули
            var projectiles = new List<(NetCoordinates, Angle, byte)>();
            var lasers = new List<(NetCoordinates, Angle, float, byte)>();

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

            // 2. Ищем лазеры (НОВАЯ ЛОГИКА через Компонент)
            // Ищем не по тегу, а по наличию компонента RadarLaserVisualsComponent
            var laserQuery = EntityManager.EntityQueryEnumerator<RadarLaserVisualsComponent, TransformComponent>();

            while (laserQuery.MoveNext(out var _, out var visuals, out var lXform))
            {
                if (lXform.MapID != mapId) continue;
                if ((_transform.GetWorldPosition(lXform) - centerPos).LengthSquared() > rangeSquared) continue;

                // Берем данные из компонента: Length и Type
                lasers.Add((GetNetCoordinates(lXform.Coordinates), visuals.Angle, visuals.Length, visuals.Type));
            }

            // 3. Отправляем сообщение
            if (projectiles.Count > 0 || lasers.Count > 0)
            {
                _uiSystem.ServerSendUiMessage(uid, RadarConsoleUiKey.Key, new RadarProjectileMessage(projectiles, lasers));
                _activeRadarConsoles.Add(uid);
            }
            else if (_activeRadarConsoles.Contains(uid))
            {
                // Если снаряды исчезли, шлем пустой список 1 раз
                _uiSystem.ServerSendUiMessage(uid, RadarConsoleUiKey.Key, new RadarProjectileMessage(projectiles, lasers));
                _activeRadarConsoles.Remove(uid);
            }
        }

    }
    // #SB AndreyCamper end
}

using Content.Server.Chat.Managers;
using Content.Shared.Atlanta.RoyalBattle.Components;
using Content.Shared.Atlanta.RoyalBattle.Systems;

namespace Content.Server.Atlanta.RoyalBattle.Systems;

public sealed class RbZoneSystem : SharedRbZoneSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RbZoneComponent, ComponentStartup>(OnComponentStartup);
    }

    protected override void MoveZone(EntityUid uid, RbZoneComponent zone, float delta)
    {
        base.MoveZone(uid, zone, delta);

        if (zone.RangeLerp <= zone.Range)
        {
            zone.RangeLerp = zone.Range;
            zone.IsMoving = false;

            zone.WaveTiming *= zone.WaveTimingMultiplier;
            zone.NextWave = zone.WaveTiming;

            // TODO: damage specify

            _chatManager.DispatchServerAnnouncement($"Следующее смещение зоны будет через {(int) zone.NextWave.TotalSeconds} секунд!", Color.Green);
        }

        Dirty(uid, zone);
    }

    protected override void TimingZone(EntityUid uid, RbZoneComponent zone, float delta)
    {
        base.TimingZone(uid, zone, delta);

        if (zone.NextWave <= TimeSpan.Zero)
        {
            zone.Range *= zone.RangeMultiplier;
            zone.RangeMultiplier *= zone.RangeRatio;

            zone.IsMoving = true;

            Dirty(uid, zone);

            _chatManager.DispatchServerAnnouncement($"Внимание! Зона вновь нестабильна: она начала сужаться!", Color.DarkRed);
        }
    }

    private void OnComponentStartup(EntityUid uid, RbZoneComponent component, ComponentStartup args)
    {
        component.RangeLerp = component.Range;
        component.NextWave = component.WaveTiming;

        var query = EntityQueryEnumerator<RbZoneCenterComponent>();

        while (query.MoveNext(out var ent, out var comp))
        {
            // I think, pointer count is one.

            component.Center = _transform.GetWorldPosition(ent);
            Sawmill.Debug($"Setup the center of zone on {component.Center} coords.");
        }

        _chatManager.DispatchServerAnnouncement($"Зона перейдёт в нестабильное состоние через {(int) component.NextWave.TotalSeconds}! Приготовьтесь!", Color.Green);

        Dirty(uid, component);
    }
}

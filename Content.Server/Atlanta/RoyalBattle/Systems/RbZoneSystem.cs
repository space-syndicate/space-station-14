using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared.Atlanta.RoyalBattle.Components;
using Content.Shared.Atlanta.RoyalBattle.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Atlanta.RoyalBattle.Systems;

public sealed class RbZoneSystem : SharedRbZoneSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RbZoneComponent, ComponentStartup>(OnComponentStartup);
    }

    protected override void ProcessUpdate(EntityUid uid, RbZoneComponent zone, float frameTime)
    {
        base.ProcessUpdate(uid, zone, frameTime);

        var currentTime = _timing.CurTime;
        if (currentTime - zone.LastDamageTime < zone.DamageTiming)
            return;
        zone.LastDamageTime = currentTime;

        var zoneCoords = _transform.GetMapCoordinates(uid);
        var playersQuery = EntityQueryEnumerator<MobStateComponent>();
        while (playersQuery.MoveNext(out var player, out var state))
        {
            if (state.CurrentState != MobState.Alive)
                continue;

            var playerCoords = _transform.GetMapCoordinates(player);

            if (playerCoords.MapId == zoneCoords.MapId)
            {
                var distance = Vector2.Distance(playerCoords.Position, zone.Center);

                if (distance >= zone.RangeLerp)
                {
                    Sawmill.Debug($"Damage {player.Id}.");
                    _damageable.TryChangeDamage(player, zone.Damage!, true);
                }
            }
        }
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
            foreach (var damage in zone.Damage!.DamageDict)
            {
                zone.Damage.DamageDict[damage.Key]
                    = zone.Damage[damage.Key] * zone.DamageMultiplier;
            }

            _chatManager.DispatchServerAnnouncement($"Следующее смещение зоны будет через {(int) zone.NextWave.TotalSeconds}с!", Color.Green);
        }

        Dirty(uid, zone);
    }

    protected override void TimingZone(EntityUid uid, RbZoneComponent zone, float delta)
    {
        base.TimingZone(uid, zone, delta);

        if (zone.NextWave <= TimeSpan.Zero && zone.WavesCount > 0)
        {
            zone.Range *= zone.RangeMultiplier;
            zone.RangeMultiplier *= zone.RangeRatio;

            zone.IsMoving = true;
            zone.WavesCount--;

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

        _chatManager.DispatchServerAnnouncement($"Зона перейдёт в нестабильное состоние через {(int) component.NextWave.TotalSeconds}с! Приготовьтесь!", Color.Green);
        component.LastDamageTime = _timing.CurTime;

        Dirty(uid, component);
    }
}

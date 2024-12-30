/*
 * Author: TornadoTech
 * License: AGPL
 */

using System.Diagnostics.CodeAnalysis;
using Content.Shared._CorvaxNext.NextVars;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station.Components;
using Robust.Server.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CorvaxNext.AutoCryoSleep;

public sealed class AutoCryoSleepSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private bool _enabled;
    private TimeSpan _disconnectedTime;
    private TimeSpan _updateTime;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoCryoSleepableComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<AutoCryoSleepableComponent, PlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_config, NextVars.AutoCryoSleepEnabled, value => _enabled = value, true);
        Subs.CVar(_config, NextVars.AutoCryoSleepTime, value => _disconnectedTime = TimeSpan.FromSeconds(value), true);
        Subs.CVar(_config, NextVars.AutoCryoSleepUpdateTime, value => _updateTime = TimeSpan.FromSeconds(value), true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + _updateTime;

        var disconnectedQuery = EntityQueryEnumerator<AutoCryoSleepComponent>();
        while (disconnectedQuery.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime - component.Disconnected < _disconnectedTime)
                continue;

            if (!_mobState.IsAlive(uid))
            {
                RemCompDeferred<AutoCryoSleepComponent>(uid);
                continue;
            }

            TryCryoSleep(uid, component.EffectId);
        }
    }

    private void OnPlayerAttached(Entity<AutoCryoSleepableComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!_enabled)
            return;

        RemCompDeferred<AutoCryoSleepComponent>(ent);
    }

    private void OnPlayerDetached(Entity<AutoCryoSleepableComponent> ent, ref PlayerDetachedEvent args)
    {
        if (!_enabled)
            return;

        var comp = EnsureComp<AutoCryoSleepComponent>(ent);
        comp.Disconnected = _timing.CurTime;
    }

    private void TryCryoSleep(EntityUid targetUid, EntProtoId? effectId = null)
    {
        if (HasComp<CryostorageContainedComponent>(targetUid))
            return;

        if (!TryGetStation(targetUid, out var stationUid))
            return;

        foreach (var cryostorageEntity in EnumerateCryostorageInSameStation(stationUid.Value))
        {
            if (!_container.TryGetContainer(cryostorageEntity, cryostorageEntity.Comp.ContainerId, out var container))
                continue;

            // We need only empty cryo sleeps
            if (!_container.CanInsert(targetUid, container))
                continue;

            if (effectId is not null)
                Spawn(effectId, Transform(targetUid).Coordinates);

            _container.Insert(targetUid, container);

            RemCompDeferred<AutoCryoSleepComponent>(targetUid);
        }
    }

    private IEnumerable<Entity<CryostorageComponent>> EnumerateCryostorageInSameStation(EntityUid stationUid)
    {
        var query = AllEntityQuery<CryostorageComponent>();
        while (query.MoveNext(out var entityUid, out var cryostorageComponent))
        {
            if (!TryGetStation(entityUid, out var cryoStationUid))
                continue;

            if (stationUid != cryoStationUid)
                continue;

            yield return (entityUid, cryostorageComponent);
        }
    }

    private bool TryGetStation(EntityUid entityUid, [NotNullWhen(true)] out EntityUid? stationUid)
    {
        stationUid = null;
        var gridUid = Transform(entityUid).GridUid;

        if (!TryComp<StationMemberComponent>(gridUid, out var stationMemberComponent))
            return false;

        stationUid = stationMemberComponent.Station;
        return true;
    }
}

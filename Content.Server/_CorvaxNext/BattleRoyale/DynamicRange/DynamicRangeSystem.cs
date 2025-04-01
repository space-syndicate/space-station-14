using System.Numerics;
using System.Linq;
using Content.Server.Salvage;
using Content.Shared._CorvaxNext.BattleRoyale.DynamicRange;
using Content.Shared.Salvage;
using Content.Server.Damage;
using Content.Server.Audio;
using Content.Server.Station.Systems;
using Content.Shared.Damage;
using Content.Shared.Audio;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Server._CorvaxNext.BattleRoyale.DynamicRange;

public sealed class DynamicRangeSystem : EntitySystem
{
    [Dependency] private readonly RestrictedRangeSystem _restrictedRange = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!; 
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DynamicRangeComponent, ComponentStartup>(OnDynamicRangeStartup);
        SubscribeLocalEvent<DynamicRangeComponent, ComponentShutdown>(OnDynamicRangeShutdown);

        SubscribeLocalEvent<DynamicRangeComponent, AfterAutoHandleStateEvent>(OnDynamicRangeChanged);

        _mapQuery = GetEntityQuery<MapComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<DynamicRangeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.OriginInitialized)
            {
                comp.Origin = new Vector2(
                    _random.NextFloat(comp.MinOriginX, comp.MaxOriginX),
                    _random.NextFloat(comp.MinOriginY, comp.MaxOriginY)
                );
                comp.OriginInitialized = true;
            }

            if (!comp.Processed)
            {
                UpdateRestrictedRange(uid, comp);
                comp.Processed = true;

                comp.PreviousRange = comp.Range;
                comp.PreviousOrigin = comp.Origin;
                comp.PreviousShrinking = comp.IsShrinking;
                comp.PreviousShrinkTime = comp.ShrinkTime;
                comp.PreviousMinRange = comp.MinimumRange;
                continue;
            }

            if (!MathHelper.CloseTo(comp.PreviousShrinkTime, comp.ShrinkTime))
            {
                var prevShrinkTime = comp.PreviousShrinkTime;
                comp.PreviousShrinkTime = comp.ShrinkTime;

                if (comp.IsShrinking && comp.ShrinkStartTime.HasValue && comp.InitialRange.HasValue)
                {
                    var elapsed = (curTime - comp.ShrinkStartTime.Value).TotalSeconds;
                    var oldProgress = elapsed / prevShrinkTime;
                    var newElapsed = oldProgress * comp.ShrinkTime;
                    comp.ShrinkStartTime = curTime - TimeSpan.FromSeconds(newElapsed);
                }
            }

            if (!MathHelper.CloseTo(comp.PreviousMinRange, comp.MinimumRange))
            {
                comp.PreviousMinRange = comp.MinimumRange;
            }

            if (comp.PreviousShrinking != comp.IsShrinking)
            {
                comp.PreviousShrinking = comp.IsShrinking;

                if (comp.IsShrinking && (!comp.ShrinkStartTime.HasValue || !comp.InitialRange.HasValue))
                {
                    comp.ShrinkStartTime = curTime;
                    comp.InitialRange = comp.Range;
                }
            }

            if (comp.IsShrinking && comp.ShrinkStartTime.HasValue && comp.InitialRange.HasValue)
            {
                var elapsed = (curTime - comp.ShrinkStartTime.Value).TotalSeconds;
                var shrinkProgress = (float)Math.Min(elapsed / comp.ShrinkTime, 1.0);

                var targetRange = Math.Max(
                    comp.MinimumRange,
                    comp.InitialRange.Value - (comp.InitialRange.Value - comp.MinimumRange) * shrinkProgress
                );

                if (!comp.PlayedShrinkMusic && shrinkProgress > 0)
                {
                    var timeToMinimum = comp.ShrinkTime * (1 - shrinkProgress);
                    
                    if (timeToMinimum <= comp.MusicStartTime)
                    {
                        PlayShrinkMusic(uid, comp);
                    }
                }

                if (Math.Abs(targetRange - comp.Range) >= 0.001f)
                {
                    comp.Range = targetRange;
                    UpdateRestrictedRange(uid, comp);
                    comp.PreviousRange = targetRange;
                }

                if (shrinkProgress >= 1.0f)
                {
                    comp.Range = comp.MinimumRange;
                    comp.IsShrinking = false;
                    comp.PreviousShrinking = false;
                }
            }

            if (!MathHelper.CloseTo(comp.PreviousRange, comp.Range) || comp.PreviousOrigin != comp.Origin)
            {
                UpdateRestrictedRange(uid, comp);

                if (comp.IsShrinking)
                {
                    comp.InitialRange = comp.Range;
                    comp.ShrinkStartTime = curTime;
                }

                comp.PreviousRange = comp.Range;
                comp.PreviousOrigin = comp.Origin;
            }

            var searchRadius = Math.Max(comp.MinSearchRange, comp.Range * comp.SearchRangeMultiplier);
            var coordinates = new EntityCoordinates(uid, comp.Origin);
            var players = _lookup.GetEntitiesInRange(coordinates, searchRadius, LookupFlags.Dynamic | LookupFlags.Approximate)
                .Where(e => HasComp<MobStateComponent>(e));

            foreach (var player in players)
            {
                var playerPos = _transform.GetWorldPosition(player);
                var distance = (playerPos - comp.Origin).Length();

                if (distance > comp.Range)
                {
                    if (!comp.LastDamageTimes.TryGetValue(player, out var lastDamage) ||
                        (curTime - lastDamage).TotalSeconds >= comp.DamageInterval)
                    {
                        var suffocationDamage = new DamageSpecifier
                        {
                            DamageDict = new Dictionary<string, FixedPoint2>
                            {
                                { comp.DamageType, FixedPoint2.New(comp.OutOfBoundsDamage) }
                            }
                        };
                        
                        _damageableSystem.TryChangeDamage(player, suffocationDamage, origin: uid);
                        comp.LastDamageTimes[player] = curTime;
                    }
                }
                else
                {
                    comp.LastDamageTimes.Remove(player);
                }
            }
        }
    }

    private void PlayShrinkMusic(EntityUid uid, DynamicRangeComponent component)
    {
        if (component.PlayedShrinkMusic)
            return;

        var selectedMusic = _audio.ResolveSound(component.ShrinkMusic);
        
        component.PlayedShrinkMusic = true;
        
        _sound.DispatchStationEventMusic(uid, selectedMusic, StationEventMusicType.Nuke);
    }

    private void OnDynamicRangeShutdown(EntityUid uid, DynamicRangeComponent component, ComponentShutdown args)
    {
        if (HasComp<RestrictedRangeComponent>(uid))
            RemComp<RestrictedRangeComponent>(uid);
    }

    private void OnDynamicRangeStartup(EntityUid uid, DynamicRangeComponent component, ComponentStartup args)
    {
        component.Processed = false;
    }

    private void OnDynamicRangeChanged(EntityUid uid, DynamicRangeComponent component, AfterAutoHandleStateEvent args)
    {
        UpdateRestrictedRange(uid, component);
    }

    public void SetRange(EntityUid uid, float range, DynamicRangeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Range = range;
        UpdateRestrictedRange(uid, component);

        if (component.IsShrinking)
        {
            component.InitialRange = range;
            component.ShrinkStartTime = _timing.CurTime;
        }

        component.PreviousRange = range;
    }

    public void SetOrigin(EntityUid uid, Vector2 origin, DynamicRangeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Origin = origin;
        component.OriginInitialized = true;
        UpdateRestrictedRange(uid, component);

        component.PreviousOrigin = origin;
    }

    public void SetShrinking(EntityUid uid, bool shrinking, bool resetMusic = false, DynamicRangeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.IsShrinking == shrinking)
            return;

        component.IsShrinking = shrinking;

        if (shrinking)
        {
            component.ShrinkStartTime = _timing.CurTime;
            component.InitialRange = component.Range;
            
            if (resetMusic)
                component.PlayedShrinkMusic = false;
        }

        component.PreviousShrinking = shrinking;
    }

    public void SetShrinkTime(EntityUid uid, float seconds, DynamicRangeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var prevShrinkTime = component.ShrinkTime;
        component.ShrinkTime = Math.Max(1f, seconds);

        if (component.IsShrinking && component.ShrinkStartTime.HasValue && component.InitialRange.HasValue)
        {
            var elapsed = (_timing.CurTime - component.ShrinkStartTime.Value).TotalSeconds;
            var oldProgress = elapsed / prevShrinkTime;
            var newElapsed = oldProgress * component.ShrinkTime;
            component.ShrinkStartTime = _timing.CurTime - TimeSpan.FromSeconds(newElapsed);
        }

        component.PreviousShrinkTime = component.ShrinkTime;
    }

    public void SetMinimumRange(EntityUid uid, float minRange, DynamicRangeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.MinimumRange = Math.Max(1f, minRange);
        component.PreviousMinRange = component.MinimumRange;
    }

    public void UpdateRestrictedRange(EntityUid uid, DynamicRangeComponent component)
    {
        var mapInitialized = false;
        var xform = _xformQuery.GetComponent(uid);
        var mapId = xform.MapID;

        if (_mapManager.MapExists(mapId))
        {
            var mapUid = _mapManager.GetMapEntityId(mapId);
            mapInitialized = _mapQuery.TryComp(mapUid, out var mapComp) && mapComp.MapInitialized;
        }

        if (!mapInitialized)
        {
            component.Processed = false;
            return;
        }

        if (TryComp<RestrictedRangeComponent>(uid, out var oldRestricted) &&
            oldRestricted.BoundaryEntity != EntityUid.Invalid &&
            !Deleted(oldRestricted.BoundaryEntity))
        {
            QueueDel(oldRestricted.BoundaryEntity);
        }

        var restricted = EnsureComp<RestrictedRangeComponent>(uid);
        restricted.Range = component.Range;
        restricted.Origin = component.Origin;

        restricted.BoundaryEntity = _restrictedRange.CreateBoundary(
            new EntityCoordinates(uid, component.Origin),
            component.Range);

        Dirty(uid, restricted);
    }
}

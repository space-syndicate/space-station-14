using Content.Shared.Atlanta.RoyalBattle.Components;

namespace Content.Shared.Atlanta.RoyalBattle.Systems;

/// <summary>
/// This handles...
/// </summary>
public abstract partial class SharedRbZoneSystem : EntitySystem
{
    protected ISawmill Sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        Sawmill = Logger.GetSawmill("Royal Battle");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RbZoneComponent>();

        while (query.MoveNext(out var uid, out var zone))
        {
            if (zone.IsMoving)
            {
                MoveZone(uid, zone, frameTime);
            }
            else
            {
                TimingZone(uid, zone, frameTime);
            }

            ProcessUpdate(uid, zone, frameTime);
        }
    }

    protected virtual void ProcessUpdate(EntityUid uid, RbZoneComponent zone, float frameTime)
    {
    }

    protected virtual void MoveZone(EntityUid uid, RbZoneComponent zone, float delta)
    {
        zone.RangeLerp -= zone.ZoneSpeed * delta;
    }

    protected virtual void TimingZone(EntityUid uid, RbZoneComponent zone, float delta)
    {
        zone.NextWave -= TimeSpan.FromSeconds(delta);
    }
}

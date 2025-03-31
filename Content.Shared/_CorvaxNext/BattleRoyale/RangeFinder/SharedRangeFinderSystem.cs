using Content.Shared.Pinpointer;
using Content.Shared.Interaction;

namespace Content.Shared._CorvaxNext.BattleRoyale.RangeFinder;

public abstract class SharedRangeFinderSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<RangeFinderComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, RangeFinderComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        ToggleRangeFinder(uid, component);
        args.Handled = true;
    }

    /// <summary>
    /// Toggles the RangeFinder.
    /// </summary>
    /// <returns>True if activated, false if deactivated.</returns>
    public virtual bool ToggleRangeFinder(EntityUid uid, RangeFinderComponent? rangeFinder = null)
    {
        if (!Resolve(uid, ref rangeFinder))
            return false;

        var isActive = !rangeFinder.IsActive;
        SetActive(uid, isActive, rangeFinder);
        return isActive;
    }

    /// <summary>
    /// Sets the active state of the RangeFinder.
    /// </summary>
    public virtual void SetActive(EntityUid uid, bool isActive, RangeFinderComponent? rangeFinder = null)
    {
        if (!Resolve(uid, ref rangeFinder))
            return;

        if (isActive == rangeFinder.IsActive)
            return;

        rangeFinder.IsActive = isActive;
        Dirty(uid, rangeFinder);
    }

    /// <summary>
    /// Sets the distance to the target.
    /// </summary>
    public void SetDistance(EntityUid uid, Distance distance, RangeFinderComponent? rangeFinder = null)
    {
        if (!Resolve(uid, ref rangeFinder))
            return;

        if (distance == rangeFinder.DistanceToTarget)
            return;

        rangeFinder.DistanceToTarget = distance;
        Dirty(uid, rangeFinder);
    }

    /// <summary>
    /// Attempts to set the arrow's direction angle.
    /// </summary>
    public bool TrySetArrowAngle(EntityUid uid, Angle arrowAngle, RangeFinderComponent? rangeFinder = null)
    {
        if (!Resolve(uid, ref rangeFinder))
            return false;

        if (rangeFinder.ArrowAngle.EqualsApprox(arrowAngle, rangeFinder.Precision))
            return false;

        rangeFinder.ArrowAngle = arrowAngle;
        Dirty(uid, rangeFinder);

        return true;
    }

    /// <summary>
    /// Updates the direction to the target.
    /// (Implemented in derived classes)
    /// </summary>
    protected virtual void UpdateDirectionToTarget(EntityUid uid, RangeFinderComponent? rangeFinder = null)
    {
    }
}

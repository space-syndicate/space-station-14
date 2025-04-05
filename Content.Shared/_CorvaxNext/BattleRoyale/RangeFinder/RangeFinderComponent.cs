using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.BattleRoyale.RangeFinder;

/// <summary>
/// Component for displaying the direction to the center of the shrinking circle in Battle Royale mode.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(SharedRangeFinderSystem))]
public sealed partial class RangeFinderComponent : Component
{
    /// <summary>
    /// The value at which the distance is considered medium.
    /// </summary>
    [DataField]
    public float MediumDistance = 16f;

    /// <summary>
    /// The value at which the distance is considered close.
    /// </summary>
    [DataField]
    public float CloseDistance = 8f;

    /// <summary>
    /// The value at which the distance is considered reached.
    /// </summary>
    [DataField]
    public float ReachedDistance = 1f;

    /// <summary>
    /// The precision of the pointer in radians.
    /// </summary>
    [DataField]
    public double Precision = 0.09;

    [ViewVariables, AutoNetworkedField]
    public bool IsActive = false;

    [ViewVariables, AutoNetworkedField]
    public Angle ArrowAngle;

    [ViewVariables, AutoNetworkedField]
    public Distance DistanceToTarget = Distance.Unknown;

    /// <summary>
    /// The tracked DynamicRange.
    /// </summary>
    [ViewVariables]
    public EntityUid? TargetRange = null;
}

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._CorvaxNext.GameTicking.Rules.Components;

/// <summary>
/// Delays adding components to the antags of a gamerule until some time has passed.
/// </summary>
/// <remarks>
/// This is used for the zombies gamemode so that new players don't hit the funny button immediately and ruin anyone else's plans.
/// </remarks>
[RegisterComponent, Access(typeof(DelayedRuleSystem))]
[AutoGenerateComponentPause]
public sealed partial class DelayedRuleComponent : Component
{
    /// <summary>
    /// The players must wait this length of time before <see cref="DelayedComponents"/> gets added.
    /// If they are somehow found out and get gibbed/cremated/etc before this delay is up they will not turn.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Delay;

    /// <summary>
    /// Whether to skip the delay if there is only 1 antag selected.
    /// </summary>
    [DataField]
    public bool IgnoreSolo;

    /// <summary>
    /// When the <see cref="Delay"/> will end.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DelayEnds;

    /// <summary>
    /// The components to add to each player's mob once the delay ends.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry DelayedComponents = new();

    /// <summary>
    /// Popup to show when the delay ends.
    /// </summary>
    [DataField(required: true)]
    public LocId EndedPopup;
}
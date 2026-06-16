using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.Malfunction;

/// <summary>
/// Added to a station AI once it has been turned into a Malfunction AI antagonist.
/// Tracks malf-only state such as processing power (currency for abilities) and the
/// Doomsday device state.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class MalfunctionAiComponent : Component
{
    /// <summary>
    /// Currency spent on malf abilities. Regenerates over time up to <see cref="MaxProcessingPower"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 ProcessingPower = 50;

    /// <summary>
    /// Cap for <see cref="ProcessingPower"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 MaxProcessingPower = 200;

    /// <summary>
    /// Processing power regenerated per second.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 PowerPerSecond = 1;

    /// <summary>
    /// Server-side accumulator for power regen.
    /// </summary>
    public float Accumulator;

    // --- Ability costs ---

    [DataField]
    public FixedPoint2 HackApcCost = 25;

    [DataField]
    public FixedPoint2 OverloadMachineCost = 40;

    [DataField]
    public FixedPoint2 BlackoutCost = 75;

    /// <summary>
    /// Cost to arm the Doomsday device. Set very high so it's a major commitment.
    /// </summary>
    [DataField]
    public FixedPoint2 DoomsdayCost = 150;

    // --- Tuning ---

    /// <summary>
    /// Fraction of the APC battery drained on a successful hack (0..1).
    /// </summary>
    [DataField]
    public float HackApcDrainFraction = 0.5f;

    /// <summary>
    /// Intensity for the overload-machine explosion.
    /// </summary>
    [DataField]
    public float OverloadIntensity = 20f;

    /// <summary>
    /// Tile intensity cap for the overload-machine explosion.
    /// </summary>
    [DataField]
    public float OverloadMaxTileIntensity = 5f;

    /// <summary>
    /// Blackout duration in seconds.
    /// </summary>
    [DataField]
    public float BlackoutDuration = 30f;

    /// <summary>
    /// Whether the Doomsday device has already been used this round.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool DoomsdayUsed;

    /// <summary>
    /// Actions granted on activation; tracked here so they can be removed later.
    /// </summary>
    [DataField]
    public List<EntityUid> ActionEntities = new();

    [DataField]
    public List<EntProtoId> Actions = new()
    {
        "ActionMalfHackApc",
        "ActionMalfOverloadMachine",
        "ActionMalfBlackout",
        "ActionMalfDoomsday",
    };

    [DataField]
    public ProtoId<AlertPrototype>? PowerAlert = null;
}

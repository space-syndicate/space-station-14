namespace Content.Server.Cursed.Atmos.Components;

/// <summary>
/// This component is used for handling entities that affect the temperature.
/// Adaptation of <see cref="TempAffectingAnomalyComponent"/> for wide usage.
/// </summary>
[RegisterComponent]
public sealed partial class TempAffectingComponent : Component
{

    /// <summary>
    /// The the amount the temperature should be modified by (negative for decreasing temp)
    /// </summary>
    [DataField("tempChangePerSecond"), ViewVariables(VVAccess.ReadWrite)]
    public float TempChangePerSecond = 0;
}

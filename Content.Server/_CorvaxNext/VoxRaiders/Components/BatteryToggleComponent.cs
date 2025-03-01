namespace Content.Server._CorvaxNext.VoxRaiders.Components;

[RegisterComponent]
public sealed partial class BatteryToggleComponent : Component
{
    [DataField]
    public float MaxCharge = 60;

    [DataField]
    public float Charge = 60;
}

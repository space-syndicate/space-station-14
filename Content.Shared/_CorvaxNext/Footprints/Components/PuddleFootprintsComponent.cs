namespace Content.Shared._CorvaxNext.Footprints.Components;

[RegisterComponent]
public sealed partial class PuddleFootprintsComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float SizeRatio = 0.2f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float OffPercent = 80f;
}

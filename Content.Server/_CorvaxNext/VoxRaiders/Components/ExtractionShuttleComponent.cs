namespace Content.Server._CorvaxNext.VoxRaiders.Components;

[RegisterComponent]
public sealed partial class ExtractionShuttleComponent : Component
{
    public HashSet<EntityUid> Owners = [];
}

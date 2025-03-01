namespace Content.Server._CorvaxNext.VoxRaiders.Components;

[RegisterComponent]
public sealed partial class ExtractionMapComponent : Component
{
    public HashSet<EntityUid> Owners = [];
}

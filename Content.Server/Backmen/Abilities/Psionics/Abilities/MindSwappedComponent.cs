namespace Content.Server.Backmen.Abilities.Psionics.Abilities;

[RegisterComponent]
public sealed partial class MindSwappedComponent : Component
{
    [ViewVariables]
    public EntityUid OriginalEntity = default!;

    [ViewVariables]
    public EntityUid OriginalMindId = default!;

    public EntityUid? MindSwapReturn = null;
}

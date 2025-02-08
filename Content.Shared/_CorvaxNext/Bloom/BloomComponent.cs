namespace Content.Shared._CorvaxNext.Bloom;

[RegisterComponent]
public sealed partial class BloomComponent : Component
{
    [DataField]
    public float Brightness = 200;

    [DataField]
    public Color Color = new(1, 1, 1, 1);
}

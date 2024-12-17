namespace Content.Server._CorvaxNext.Traits.Assorted;

/// <summary>
///     Used for traits that add a starting moodlet.
/// </summary>
[RegisterComponent]
public sealed partial class MoodModifyTraitComponent : Component
{
    [DataField]
    public string? MoodId;

    [DataField]
    public float GoodMoodMultiplier = 1.0f;

    [DataField]
    public float BadMoodMultiplier = 1.0f;
}

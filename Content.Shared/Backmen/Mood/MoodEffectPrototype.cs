using Robust.Shared.Prototypes;

namespace Content.Shared._CorvaxNext.Mood;

[Prototype]
public sealed class MoodEffectPrototype : IPrototype
{
    /// <summary>
    ///     The ID of the moodlet to use.
    /// </summary>
    [IdDataField]
    public string ID { get; } = default!;

    public string Description => Loc.GetString($"mood-effect-{ID}");
    /// <summary>
    ///     If they already have an effect with the same category, the new one will replace the old one.
    /// </summary>
    [DataField, ValidatePrototypeId<MoodCategoryPrototype>]
    public string? Category;
    /// <summary>
    ///     How much should this moodlet modify an entity's Mood.
    /// </summary>
    [DataField(required: true)]
    public float MoodChange;
    /// <summary>
    ///     How long, in Seconds, does this moodlet last? If omitted, the moodlet will last until canceled by any system.
    /// </summary>
    [DataField]
    public int Timeout;
    /// <summary>
    ///     Should this moodlet be hidden from the player? EG: No popups or chat messages.
    /// </summary>
    [DataField]
    public bool Hidden;
}

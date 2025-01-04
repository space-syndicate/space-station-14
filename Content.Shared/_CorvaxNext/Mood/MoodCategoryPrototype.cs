using Robust.Shared.Prototypes;

namespace Content.Shared._CorvaxNext.Mood;

/// <summary>
///     A prototype defining a category for moodlets, where only a single moodlet of a given category is permitted.
/// </summary>
[Prototype]
public sealed class MoodCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;
}

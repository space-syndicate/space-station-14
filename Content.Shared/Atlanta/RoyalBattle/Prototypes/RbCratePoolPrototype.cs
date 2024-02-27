using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atlanta.RoyalBattle.Prototypes;

/// <summary>
/// Uses to setup royal battles crates content.
/// </summary>
[Prototype("rbCratePool")]
public sealed partial class RbCratePoolPrototype : IWeightedRandomPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("weights")]
    public Dictionary<string, float> Weights { get; private set; } = new();
}

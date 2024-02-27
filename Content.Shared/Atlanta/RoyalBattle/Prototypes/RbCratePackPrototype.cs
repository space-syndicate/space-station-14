using Robust.Shared.Prototypes;

namespace Content.Shared.Atlanta.RoyalBattle.Prototypes;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype("rbCratePack")]
public sealed partial class RbCratePackPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("pack", required: true)]
    public List<EntProtoId> Pack = new();
}

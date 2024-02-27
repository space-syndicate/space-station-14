using Content.Shared.Atlanta.RoyalBattle.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atlanta.RoyalBattle.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class RbCrateRandomComponent : Component
{
    [DataField("content", required: true)]
    public ProtoId<RbCratePoolPrototype> Content;
}

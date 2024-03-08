using Robust.Shared.Prototypes;

namespace Content.Server.Atlanta.RoyalBattle.Components;

[RegisterComponent]
public sealed partial class RandomRbCrateSpawnerComponent : Component
{
    [DataField("proto")]
    public EntProtoId PrototypeId = "RandomCrateRoyalBattle";
}

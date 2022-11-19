using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.SecretStationGoal;

[Serializable, Prototype("secretStationGoal")]
public sealed class SecretStationGoalPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField("text")] public string Text { get; set; } = string.Empty;
}

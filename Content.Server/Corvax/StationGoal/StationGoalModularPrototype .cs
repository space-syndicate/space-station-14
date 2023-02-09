using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.StationGoal
{
    [Serializable, Prototype("stationGoalModular")]
    public sealed class StationGoalModularPrototype : IPrototype
    {
        [IdDataFieldAttribute] public string ID { get; } = default!;

        [DataField("text")] public string Text { get; set; } = string.Empty;
    }
}

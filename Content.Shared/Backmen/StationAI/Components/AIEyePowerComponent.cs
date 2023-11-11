using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent]
public sealed partial class AIEyePowerComponent : Component
{
    [DataField("prototype")]
    public EntProtoId Prototype = "AIEye";

    public EntityUid? EyePowerAction = null;
}

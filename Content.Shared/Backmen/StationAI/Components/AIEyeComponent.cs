using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent]
public sealed partial class AIEyeComponent : Component
{
    public Entity<StationAIComponent>? AiCore;

    public EntProtoId ReturnAction = "AIEyeReturnAction";

    public EntityUid? ReturnActionUid;
}

public sealed partial class AIEyePowerActionEvent : InstantActionEvent
{

}

public sealed partial class AIEyePowerReturnActionEvent : InstantActionEvent
{
}

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class AIEyeComponent : Component
{
    public Entity<StationAIComponent>? AiCore;

    [DataField]
    public EntProtoId ReturnAction = "AIEyeReturnAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ReturnActionUid;
}

public sealed partial class AIEyePowerActionEvent : InstantActionEvent
{

}

public sealed partial class AIEyePowerReturnActionEvent : InstantActionEvent
{
}

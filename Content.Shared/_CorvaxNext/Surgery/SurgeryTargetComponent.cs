using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxNext.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTargetComponent : Component
{
    [DataField]
    public bool CanOperate = true;
}

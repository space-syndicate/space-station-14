using Robust.Shared.Prototypes;

namespace Content.Shared._CorvaxNext.TurretControl.Components;

[RegisterComponent]
public sealed partial class TurretControllerComponent : Component
{
    [DataField]
    public ComponentRegistry RequiredComponents = [];
}

using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxNext.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class DrillComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a drill";
    public bool? Used { get; set; } = null;

    [DataField]
    public float Speed { get; set; } = 1f;
}

using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxNext.Overlays;

[RegisterComponent, NetworkedComponent]
public sealed partial class ThermalVisionComponent : SwitchableOverlayComponent
{
    public override string? ToggleAction { get; set; } = "ToggleThermalVision";

    public override Color Color { get; set; } = Color.FromHex("#C26E4A");
}

public sealed partial class ToggleThermalVisionEvent : InstantActionEvent;

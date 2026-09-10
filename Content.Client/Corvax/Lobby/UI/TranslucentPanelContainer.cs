using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Corvax.Lobby.UI;

/// <summary>
/// Applies opacity only to the panel background while preserving the tint
/// supplied by the active UI stylesheet and leaving child controls opaque.
/// </summary>
public sealed class TranslucentPanelContainer : PanelContainer
{
    public float BackgroundOpacity { get; set; } = 1f;

    protected override void Draw(DrawingHandleScreen handle)
    {
        var previous = handle.Modulate;
        handle.Modulate = previous.WithAlpha(previous.A * BackgroundOpacity);
        base.Draw(handle);
        handle.Modulate = previous;
    }
}

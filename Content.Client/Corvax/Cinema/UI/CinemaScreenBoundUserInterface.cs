using Content.Shared.Corvax.Cinema;
using Robust.Shared.GameObjects;

namespace Content.Client.Corvax.Cinema.UI;

/// <summary>
/// Opens the cinema control window when the screen is clicked (via ActivatableUI). The window itself
/// is client-side only — changes are sent through <see cref="MsgCinemaScreenControl"/> and validated
/// by the server.
/// </summary>
public sealed class CinemaScreenBoundUserInterface : BoundUserInterface
{
    private CinemaScreenControlWindow? _window;

    public CinemaScreenBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<CinemaScreenComponent>(Owner, out var comp))
            return;

        _window = new CinemaScreenControlWindow(EntMan.GetNetEntity(Owner), comp.VideoUrl, comp.Volume);
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _window == null)
            return;

        _window.OnClose -= Close;
        _window.Close();
        _window = null;
    }
}

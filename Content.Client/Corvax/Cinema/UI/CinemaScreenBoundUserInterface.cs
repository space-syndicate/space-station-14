using Content.Shared.Corvax.Cinema;
using Content.Client.Administration.Managers;
using Robust.Shared.GameObjects;

namespace Content.Client.Corvax.Cinema.UI;

/// <summary>
/// Opens the cinema control window when the screen is clicked (via ActivatableUI). The window itself
/// is client-side only — changes are sent through <see cref="CinemaScreenControlMessage"/> and validated
/// by the server.
/// </summary>
public sealed partial class CinemaScreenBoundUserInterface : BoundUserInterface
{
    [Dependency] private IClientAdminManager _admin = default!;

    private CinemaScreenControlWindow? _window;

    public CinemaScreenBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<CinemaScreenComponent>(Owner, out var comp))
            return;

        _window = new CinemaScreenControlWindow(comp.VideoUrl, comp.Volume);
        _admin.AdminStatusUpdated += UpdateAdmin;
        UpdateAdmin();
        _window.OnControl += SendMessage;
        _window.OnClose += Close;
        if (State is CinemaScreenState state)
            _window.UpdateState(state);
        _window.OpenCentered();
    }

    private void UpdateAdmin() => _window?.SetAdmin(_admin.IsActive());

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CinemaScreenState screen)
            _window?.UpdateState(screen);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _window == null)
            return;

        _admin.AdminStatusUpdated -= UpdateAdmin;
        _window.OnClose -= Close;
        _window.OnControl -= SendMessage;
        _window.Close();
        _window = null;
    }
}

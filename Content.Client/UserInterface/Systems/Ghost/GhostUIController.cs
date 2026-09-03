using Content.Client.Corvax.Ghost;
using Content.Client.Gameplay;
using Content.Client.Ghost;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;

namespace Content.Client.UserInterface.Systems.Ghost;

// TODO hud refactor BEFORE MERGE fix ghost gui being too far up
public sealed partial class GhostUIController : UIController, IOnSystemChanged<GhostSystem>
{
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!; // Corvax-GoLobby

    [UISystemDependency] private readonly GhostSystem? _system = default;

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    private GhostGoLobbyConfirmWindow? _goLobbyConfirmWindow; // Corvax-GoLobby
    private bool _goLobbyEnabled = true; // Corvax-GoLobby

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        // Corvax-GoLobby
        _cfg.OnValueChanged(CCCVars.GhostGoLobbyEnabled, OnGoLobbyEnabledChanged, true);
    }

    private void OnGoLobbyEnabledChanged(bool enabled) // Corvax-GoLobby
    {
        _goLobbyEnabled = enabled;
        UpdateGui();
    }

    private void OnScreenLoad()
    {
        LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnSystemLoaded(GhostSystem system)
    {
        system.PlayerRemoved += OnPlayerRemoved;
        system.PlayerUpdated += OnPlayerUpdated;
        system.PlayerAttached += OnPlayerAttached;
        system.PlayerDetached += OnPlayerDetached;
        system.GhostWarpsResponse += OnWarpsResponse;
        system.GhostRoleCountUpdated += OnRoleCountUpdated;
    }

    public void OnSystemUnloaded(GhostSystem system)
    {
        system.PlayerRemoved -= OnPlayerRemoved;
        system.PlayerUpdated -= OnPlayerUpdated;
        system.PlayerAttached -= OnPlayerAttached;
        system.PlayerDetached -= OnPlayerDetached;
        system.GhostWarpsResponse -= OnWarpsResponse;
        system.GhostRoleCountUpdated -= OnRoleCountUpdated;
    }

    public void UpdateGui()
    {
        if (Gui == null)
        {
            return;
        }

        Gui.Visible = _system?.IsGhost ?? false;
        Gui.Update(_system?.AvailableGhostRoleCount, _system?.Player?.CanReturnToBody, _goLobbyEnabled); // Corvax-GoLobby edit
    }

    private void OnPlayerRemoved(GhostComponent component)
    {
        Gui?.Hide();
    }

    private void OnPlayerUpdated(GhostComponent component)
    {
        UpdateGui();
    }

    private void OnPlayerAttached(GhostComponent component)
    {
        if (Gui == null)
            return;

        Gui.Visible = true;
        UpdateGui();
    }

    private void OnPlayerDetached()
    {
        Gui?.Hide();
    }

    private void OnWarpsResponse(GhostWarpsResponseEvent msg)
    {
        if (Gui?.TargetWindow is not { } window)
            return;

        window.UpdateWarps(msg.Warps);
        window.Populate();
    }

    private void OnRoleCountUpdated(GhostUpdateGhostRoleCountEvent msg)
    {
        UpdateGui();
    }

    private void OnWarpClicked(NetEntity player)
    {
        var msg = new GhostWarpToTargetRequestEvent(player);
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnGhostnadoClicked()
    {
        var msg = new GhostnadoRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnWarpToRandomFollowedClicked()
    {
        var msg = new WarpToRandomFollowedRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnWarpToRandomClicked()
    {
        var msg = new WarpToRandomRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    public void LoadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed += RequestWarps;
        Gui.ReturnToBodyPressed += ReturnToBody;
        Gui.GhostRolesPressed += GhostRolesPressed;
        Gui.TargetWindow.WarpClicked += OnWarpClicked;
        Gui.TargetWindow.OnGhostnadoClicked += OnGhostnadoClicked;
        Gui.TargetWindow.OnWarpToRandomFollowedClicked += OnWarpToRandomFollowedClicked;
        Gui.TargetWindow.OnWarpToRandomClicked += OnWarpToRandomClicked;
        Gui.GhostGoLobbyPressed += GhostGoLobby; // Corvax-GoLobby

        UpdateGui();
    }

    public void UnloadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed -= RequestWarps;
        Gui.ReturnToBodyPressed -= ReturnToBody;
        Gui.GhostRolesPressed -= GhostRolesPressed;
        Gui.TargetWindow.WarpClicked -= OnWarpClicked;
        Gui.GhostGoLobbyPressed -= GhostGoLobby; // Corvax-GoLobby

        Gui.Hide();
    }

    private void ReturnToBody()
    {
        _system?.ReturnToBody();
    }

    // Corvax-Changes-Start
    private void GhostGoLobby()
    {
        if (_goLobbyConfirmWindow is { Disposed: false })
        {
            _goLobbyConfirmWindow.MoveToFront();
            return;
        }

        _goLobbyConfirmWindow = new GhostGoLobbyConfirmWindow();
        _goLobbyConfirmWindow.ContinuePressed += () => _system?.GhostGoLobby();
        _goLobbyConfirmWindow.OnClose += () => _goLobbyConfirmWindow = null;
        _goLobbyConfirmWindow.OpenCentered();
    }
    // Corvax-Changes-End

    private void RequestWarps()
    {
        _system?.RequestWarps();
        Gui?.TargetWindow.Populate();
        Gui?.TargetWindow.OpenCentered();
    }

    private void GhostRolesPressed()
    {
        _system?.OpenGhostRoles();
    }
}

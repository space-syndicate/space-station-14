using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.Events;
using Content.Shared.Corvax.Ghost;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Corvax.Ghost;

public sealed partial class GhostGoLobbyButton : Button
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color Waiting = Color.FromHex("#8B2E2E");
    private static readonly Color Ready = Color.FromHex("#2E8B3E");

    private GhostGoLobbyConfirmWindow? _confirmWindow;

    public GhostGoLobbyButton()
    {
        IoCManager.InjectDependencies(this);

        OnPressed += _ => OnPress();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _cfg.OnValueChanged(CCCVars.GhostGoLobbyEnabled, OnEnabledChanged, true);
    }

    protected override void ExitedTree()
    {
        _cfg.UnsubValueChanged(CCCVars.GhostGoLobbyEnabled, OnEnabledChanged);
        _confirmWindow?.Close();
        base.ExitedTree();
    }

    private void OnEnabledChanged(bool enabled)
    {
        Visible = enabled;
    }

    private void OnPress()
    {
        if (_confirmWindow is { Disposed: false })
        {
            _confirmWindow.MoveToFront();
            return;
        }

        _confirmWindow = new GhostGoLobbyConfirmWindow();
        _confirmWindow.ContinuePressed += () => _net.SendSystemNetworkMessage(new GhostGoLobbyEvent());
        _confirmWindow.OnClose += () => _confirmWindow = null;
        _confirmWindow.OpenCentered();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var remaining = _players.LocalEntity is { } player
            && _entMan.TryGetComponent<GhostGoLobbyComponent>(player, out var lobby)
                ? lobby.AvailableAt - _timing.CurTime
                : TimeSpan.Zero;

        if (remaining > TimeSpan.Zero)
        {
            var text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            Text = Loc.GetString("ghost-go-lobby-button-waiting", ("time", text));
            Disabled = true;
            ModulateSelfOverride = Waiting;
        }
        else
        {
            Text = Loc.GetString("ghost-go-lobby-button");
            Disabled = false;
            ModulateSelfOverride = Ready;
        }
    }
}

using System.Numerics;
using Content.Shared.Corvax.Cinema;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;

namespace Content.Client.Corvax.Cinema.UI;

/// <summary>
/// Small control panel for a cinema screen: URL, Play/Pause/Stop, Seek and Volume.
/// Every action is sent to the server, which is the authority and re-validates the sender (admin only).
/// </summary>
public sealed partial class CinemaScreenControlWindow : BaseWindow
{
    [Dependency] private IClientNetManager _netManager = default!;

    private readonly NetEntity _target;

    private readonly LineEdit _urlEdit;
    private readonly LineEdit _seekEdit;
    private readonly Slider _volumeSlider;

    public CinemaScreenControlWindow(NetEntity target, string? currentUrl, float currentVolume)
    {
        IoCManager.InjectDependencies(this);
        _target = target;

        Resizable = false;
        SetSize = new Vector2(360, 240);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
        };

        // Header.
        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
        };
        header.AddChild(new Label
        {
            Text = "Cinema Screen",
            HorizontalExpand = true,
        });
        var close = new Button { Text = "X" };
        close.OnPressed += _ => Close();
        header.AddChild(close);
        root.AddChild(header);

        // URL.
        root.AddChild(new Label { Text = "Video URL (HTTPS, whitelisted):" });
        var urlRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _urlEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = "https://...",
            Text = currentUrl ?? string.Empty,
        };
        urlRow.AddChild(_urlEdit);
        var setUrl = new Button { Text = "Set" };
        setUrl.OnPressed += _ => Send(CinemaScreenAction.SetUrl, url: _urlEdit.Text);
        urlRow.AddChild(setUrl);
        root.AddChild(urlRow);

        // Transport.
        var transport = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var play = new Button { Text = "Play", HorizontalExpand = true };
        play.OnPressed += _ => Send(CinemaScreenAction.Play, url: _urlEdit.Text);
        var pause = new Button { Text = "Pause", HorizontalExpand = true };
        pause.OnPressed += _ => Send(CinemaScreenAction.Pause);
        var stop = new Button { Text = "Stop", HorizontalExpand = true };
        stop.OnPressed += _ => Send(CinemaScreenAction.Stop);
        transport.AddChild(play);
        transport.AddChild(pause);
        transport.AddChild(stop);
        root.AddChild(transport);

        // Seek.
        var seekRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        seekRow.AddChild(new Label { Text = "Seek (s):" });
        _seekEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = "0",
        };
        seekRow.AddChild(_seekEdit);
        var seek = new Button { Text = "Seek" };
        seek.OnPressed += _ =>
        {
            if (double.TryParse(_seekEdit.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                Send(CinemaScreenAction.Seek, seek: seconds);
        };
        seekRow.AddChild(seek);
        root.AddChild(seekRow);

        // Volume.
        var volumeRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        volumeRow.AddChild(new Label { Text = "Volume:" });
        _volumeSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = currentVolume,
            HorizontalExpand = true,
        };
        _volumeSlider.OnValueChanged += range => Send(CinemaScreenAction.SetVolume, volume: range.Value);
        volumeRow.AddChild(_volumeSlider);
        root.AddChild(volumeRow);

        // Wrap the content in a panel so the window has a proper background/border (BaseWindow has none).
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1E1E22"),
                BorderColor = Color.FromHex("#5A5A64"),
                BorderThickness = new Thickness(2),
            },
        };
        panel.AddChild(root);

        AddChild(panel);
    }

    private void Send(CinemaScreenAction action, string? url = null, double seek = 0, float volume = 1f)
    {
        Log.Info($"Cinema UI send: action={action}, target={_target}, url='{url}', seek={seek}, volume={volume}");
        _netManager.ClientSendMessage(new MsgCinemaScreenControl
        {
            Entity = _target,
            Action = action,
            Url = url ?? string.Empty,
            SeekSeconds = seek,
            Volume = volume,
        });
    }
}

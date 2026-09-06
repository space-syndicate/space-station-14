using System.Linq;
using Content.Shared.Corvax.Cinema;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Corvax.Cinema.UI;

/// <summary>
/// Small control panel for a cinema screen: URL, Play/Pause/Stop, Seek and Volume.
/// Every action is sent to the server, which is the authority and validates player interaction and administrator access to custom URLs.
/// </summary>
public sealed partial class CinemaScreenControlWindow : DefaultWindow
{
    public event Action<CinemaScreenControlMessage>? OnControl;
    private readonly RichTextLabel _status = new();
    private string? _currentUrl;
    private bool _updating;
    private bool _isAdmin;
    private readonly BoxContainer _customUrl = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly OptionButton _films = new() { HorizontalExpand = true };
    private readonly Button _selectFilm = new() { Text = Loc.GetString("cinema-watch") };
    private string[] _filmIds = [];


    private readonly LineEdit _urlEdit;
    private readonly LineEdit _seekEdit;
    private readonly Slider _volumeSlider;

    public CinemaScreenControlWindow(string? currentUrl, float currentVolume)
    {
        _currentUrl = currentUrl;
        Title = Loc.GetString("cinema-title");

        Resizable = false;
        SetWidth = 500;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
        };

        var help = new RichTextLabel();
        help.SetMessage(Loc.GetString("cinema-help"));
        root.AddChild(help);
        root.AddChild(_status);

        root.AddChild(new Label { Text = Loc.GetString("cinema-films") });
        var filmRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _films.OnItemSelected += args => _films.SelectId(args.Id);
        _selectFilm.OnPressed += _ =>
        {
            if (_films.SelectedId >= 0 && _films.SelectedId < _filmIds.Length)
                Send(CinemaScreenAction.SelectFilm, film: _filmIds[_films.SelectedId]);
        };
        filmRow.AddChild(_films);
        filmRow.AddChild(_selectFilm);
        root.AddChild(filmRow);

        // Custom URLs are an additional administrator control.
        _customUrl.AddChild(new Label { Text = Loc.GetString("cinema-url") });
        var urlRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _urlEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = "https://...",
            Text = currentUrl ?? string.Empty,
        };
        urlRow.AddChild(_urlEdit);
        var setUrl = new Button { Text = Loc.GetString("cinema-set") };
        setUrl.OnPressed += _ => Send(CinemaScreenAction.SetUrl, url: _urlEdit.Text);
        urlRow.AddChild(setUrl);
        _customUrl.AddChild(urlRow);
        root.AddChild(_customUrl);

        // Transport.
        var transport = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var play = new Button { Text = Loc.GetString("cinema-play"), HorizontalExpand = true };
        play.OnPressed += _ => Send(CinemaScreenAction.Play, url: _isAdmin ? _urlEdit.Text : null);
        var pause = new Button { Text = Loc.GetString("cinema-pause"), HorizontalExpand = true };
        pause.OnPressed += _ => Send(CinemaScreenAction.Pause);
        var stop = new Button { Text = Loc.GetString("cinema-stop"), HorizontalExpand = true };
        stop.OnPressed += _ => Send(CinemaScreenAction.Stop);
        transport.AddChild(play);
        transport.AddChild(pause);
        transport.AddChild(stop);
        root.AddChild(transport);

        // Seek.
        var seekRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        seekRow.AddChild(new Label { Text = Loc.GetString("cinema-seek-label") });
        _seekEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = "0",
        };
        seekRow.AddChild(_seekEdit);
        var seek = new Button { Text = Loc.GetString("cinema-seek") };
        seek.OnPressed += _ =>
        {
            if (double.TryParse(_seekEdit.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds) && seconds >= 0)
                Send(CinemaScreenAction.Seek, seek: seconds);
        };
        seekRow.AddChild(seek);
        root.AddChild(seekRow);

        // Volume.
        var volumeRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        volumeRow.AddChild(new Label { Text = Loc.GetString("cinema-volume") });
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

        Contents.AddChild(root);
    }

    public void SetAdmin(bool isAdmin)
    {
        _isAdmin = isAdmin;
        _customUrl.Visible = isAdmin;
    }

    public void UpdateState(CinemaScreenState state)
    {
        if (!_filmIds.SequenceEqual(state.Films))
        {
            _filmIds = state.Films;
            _films.Clear();
            for (var i = 0; i < _filmIds.Length; i++)
                _films.AddItem(Loc.GetString(_filmIds[i]), i);
            if (_filmIds.Length > 0)
                _films.SelectId(0);
        }
        _selectFilm.Disabled = _filmIds.Length == 0;
        if (_currentUrl != state.Url)
        {
            _currentUrl = state.Url;
            _urlEdit.Text = state.Url ?? string.Empty;
        }
        _updating = true;
        _volumeSlider.Value = state.Volume;
        _updating = false;
        _status.SetMessage(Loc.GetString(state.Status));
    }

    private void Send(CinemaScreenAction action, string? url = null, double seek = 0, float volume = 1f, string film = "")
    {
        if (_updating)
            return;

        OnControl?.Invoke(new CinemaScreenControlMessage
        {
            Action = action,
            Film = film,
            Url = url ?? string.Empty,
            SeekSeconds = seek,
            Volume = volume,
        });
    }
}

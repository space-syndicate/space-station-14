using System.IO;
using Content.Client.Corvax.AdminMusic.UI;
using Content.Client.Eui;
using Content.Shared.Corvax.AdminMusic;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Eui;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Client.Corvax.AdminMusic;

public sealed partial class AdminMusicEui : BaseEui
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IFileDialogManager _fileDialog = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly AdminMusicSystem _music;
    private readonly AdminMusicLibrarySystem _library;
    private readonly AdminMusicWindow _window;
    private AdminMusicPlaySettingsWindow? _settingsWindow;
    private AdminMusicEuiState? _lastState;

    /// <summary>
    ///     Данные треков, ожидающих разрешения сервера на загрузку.
    /// </summary>
    private readonly Dictionary<Guid, byte[]> _pendingUploads = new();

    private bool _adding;

    public AdminMusicEui()
    {
        IoCManager.InjectDependencies(this);

        _music = _entManager.System<AdminMusicSystem>();
        _library = _entManager.System<AdminMusicLibrarySystem>();

        _window = new AdminMusicWindow();
        _window.SetVolumeLimits(MinVolume, MaxVolume);
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnAddPressed += AddTrack;
        _window.OnDeletePressed += DeleteTrack;
        _window.OnSettingsPressed += OpenSettings;
        _window.OnSetPaused += (id, paused) => SendMessage(new AdminMusicEuiMsg.SetPaused { TrackId = id, Paused = paused });
        _window.OnSeek += (id, position) => SendMessage(new AdminMusicEuiMsg.Seek { TrackId = id, Position = position });
        _window.OnSetVolume += (id, volume) => SendMessage(new AdminMusicEuiMsg.SetVolume { TrackId = id, Volume = volume });
        _window.OnStop += id => SendMessage(new AdminMusicEuiMsg.Stop { TrackId = id });

        _library.LibraryChanged += OnLibraryChanged;
        _music.PreviewStopped += OnPreviewStopped;
    }

    private float MinVolume => _cfg.GetCVar(CCCVars.AdminMusicMinVolume);
    private float MaxVolume => Math.Max(MinVolume, _cfg.GetCVar(CCCVars.AdminMusicMaxVolume));

    public override void Opened()
    {
        base.Opened();

        _window.SetLibrary(_library.Entries);
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        _library.LibraryChanged -= OnLibraryChanged;
        _music.PreviewStopped -= OnPreviewStopped;
        _music.StopPreview();

        _settingsWindow?.Close();
        _window.Close();
        _pendingUploads.Clear();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AdminMusicEuiState musicState)
            return;

        _lastState = musicState;
        _window.UpdateActiveTracks(musicState.Tracks);
        _settingsWindow?.UpdateState(musicState);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case AdminMusicEuiMsg.UploadPermitted permitted:
                if (_pendingUploads.Remove(permitted.TrackId, out var data))
                    _music.Upload(permitted.TrackId, data);

                break;

            case AdminMusicEuiMsg.Error error:
                // Запрос, на который пришла ошибка, уже не будет одобрен.
                if (error.TrackId is { } trackId)
                    _pendingUploads.Remove(trackId);

                _window.SetStatus(error.Message);
                _settingsWindow?.SetError(error.Message);
                break;
        }
    }

    private void OnLibraryChanged()
    {
        _window.SetLibrary(_library.Entries);
    }

    #region Library

    private async void AddTrack()
    {
        if (_adding)
            return;

        _adding = true;
        try
        {
            var filters = new FileDialogFilters(new FileDialogFilters.Group("ogg"));
            var (stream, name) = await _fileDialog.GetFileAndName(filters, FileAccess.Read);
            if (stream == null)
                return;

            await using (stream)
            {
                var result = _library.AddTrack(stream, name, out _);
                _window.SetStatus(result switch
                {
                    AdminMusicLibrarySystem.AddResult.TooLarge => Loc.GetString("admin-music-error-too-large",
                        ("max", _cfg.GetCVar(CCCVars.AdminMusicMaxSizeMb))),
                    AdminMusicLibrarySystem.AddResult.Invalid => Loc.GetString("admin-music-error-invalid-file"),
                    _ => null,
                });
            }
        }
        catch (Exception e)
        {
            _logManager.GetSawmill("admin-music").Error($"Failed to add admin music track: {e}");
            _window.SetStatus(Loc.GetString("admin-music-error-generic"));
        }
        finally
        {
            _adding = false;
        }
    }

    private void DeleteTrack(AdminMusicLibraryEntry entry)
    {
        if (_settingsWindow?.Entry == entry)
            _settingsWindow.Close();

        _library.RemoveTrack(entry);
    }

    #endregion

    #region Settings

    private void OpenSettings(AdminMusicLibraryEntry entry)
    {
        _settingsWindow?.Close();

        var window = new AdminMusicPlaySettingsWindow(entry, MinVolume, MaxVolume);
        window.OnPreviewPressed += TogglePreview;
        window.OnPlayPressed += RequestPlay;
        window.OnRefreshPressed += () => SendMessage(new AdminMusicEuiMsg.Refresh());
        window.OnClose += () =>
        {
            if (_settingsWindow != window)
                return;

            _music.StopPreview();
            _settingsWindow = null;
        };

        _settingsWindow = window;

        if (_lastState != null)
            window.UpdateState(_lastState);

        window.OpenCentered();

        // Обновляем положение игроков при открытии настроек.
        SendMessage(new AdminMusicEuiMsg.Refresh());
    }

    private void TogglePreview(AdminMusicPlaySettingsWindow window)
    {
        if (_music.IsPreviewing)
        {
            _music.StopPreview();
            return;
        }

        if (_library.GetTrackData(window.Entry) is not { } data)
        {
            window.SetError(Loc.GetString("admin-music-error-file-missing"));
            return;
        }

        var volume = Math.Clamp(window.Volume, MinVolume, MaxVolume);
        if (_music.StartPreview(data, window.Start, window.End, volume))
            window.SetPreviewing(true);
        else
            window.SetError(Loc.GetString("admin-music-error-invalid-file"));
    }

    private void OnPreviewStopped()
    {
        _settingsWindow?.SetPreviewing(false);
    }

    private void RequestPlay(AdminMusicPlaySettingsWindow window)
    {
        if (window.SelectedPlayers.Count == 0)
            return;

        if (_library.GetTrackData(window.Entry) is not { } data)
        {
            window.SetError(Loc.GetString("admin-music-error-file-missing"));
            return;
        }

        _music.StopPreview();
        window.SetError(null);
        _window.SetStatus(null);

        var trackId = Guid.NewGuid();
        _pendingUploads[trackId] = data;

        SendMessage(new AdminMusicEuiMsg.RequestPlay
        {
            TrackId = trackId,
            Title = window.Entry.Title,
            Size = data.Length,
            Volume = Math.Clamp(window.Volume, MinVolume, MaxVolume),
            Start = window.Start,
            End = window.End,
            StartPaused = window.StartPaused,
            Recipients = new List<NetUserId>(window.SelectedPlayers),
        });

        window.Close();
        _window.ShowActiveTab();
    }

    #endregion
}

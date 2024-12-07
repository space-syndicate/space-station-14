using Content.Client.Eui;
using Content.Client._CorvaxNext.Administration.UI.Audio.Widgets;
using Content.Shared.Eui;
using Content.Shared._CorvaxNext.Administration.UI.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Client._CorvaxNext.Administration.UI.Audio;

public sealed partial class AdminAudioPanelEui : BaseEui
{
    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    private SharedAudioSystem _audioSystem;

    private AdminAudioPanelEuiState? _state = null;
    private AdminAudioPanel? _adminAudioPanel = null;

    public AdminAudioPanelEui() : base()
    {
        IoCManager.InjectDependencies(this);
        _audioSystem = _entitySystem.GetEntitySystem<SharedAudioSystem>();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is AdminAudioPanelEuiState adminAudioPanelState)
        {
            _state = adminAudioPanelState;
            UpdateUI();
        }
    }

    public override void Opened()
    {
        _adminAudioPanel = new AdminAudioPanel();
        _adminAudioPanel.OpenCentered();

        _adminAudioPanel.OnPlayButtonEnabled += () => Play();
        _adminAudioPanel.OnPauseButtonEnabled += () => Pause();
        _adminAudioPanel.OnStopButtonEnabled += () => Stop();
        _adminAudioPanel.OnAddTrackPressed += (track) => AddTrack(track);
        _adminAudioPanel.OnPlaybackReleased += (ratio) => SetPlayback(ratio);
        _adminAudioPanel.OnGlobalCheckboxToggled += (toggled) => ChangeGlobalToggled(toggled);
        _adminAudioPanel.OnVolumeLineTextChanged += (volume) => SetVolume(volume);
        _adminAudioPanel.OnSelectPlayer += (guid) => SelectPlayer(guid);
        _adminAudioPanel.OnUnselectPlayer += (guid) => UnselectPlayer(guid);
    }

    public override void Closed()
    {
        if (_adminAudioPanel != null)
            _adminAudioPanel.Close();
    }

    public void Play()
    {
        var message = new AdminAudioPanelEuiMessage.Play();
        SendMessage(message);
    }

    public void Stop()
    {
        var message = new AdminAudioPanelEuiMessage.Stop();
        SendMessage(message);
    }

    public void Pause()
    {
        var message = new AdminAudioPanelEuiMessage.Pause();
        SendMessage(message);
    }

    public void SetPlayback(float ratio)
    {
        var message = new AdminAudioPanelEuiMessage.SetPlaybackPosition(ratio);
        SendMessage(message);
    }

    public void AddTrack(string track)
    {
        var message = new AdminAudioPanelEuiMessage.AddTrack(track);
        SendMessage(message);
    }

    public void ChangeGlobalToggled(bool toggled)
    {
        var message = new AdminAudioPanelEuiMessage.GlobalToggled(toggled);
        SendMessage(message);
    }

    public void SetVolume(float volume)
    {
        var message = new AdminAudioPanelEuiMessage.SetVolume(volume);
        SendMessage(message);
    }

    private void SelectPlayer(Guid player)
    {
        var message = new AdminAudioPanelEuiMessage.SelectPlayer(player);
        SendMessage(message);
    }

    private void UnselectPlayer(Guid player)
    {
        var message = new AdminAudioPanelEuiMessage.UnselectPlayer(player);
        SendMessage(message);
    }

    private void UpdateUI()
    {
        if (_adminAudioPanel is not { })
            return;

        if (_state is not { })
            return;

        var audioEntity = _entity.GetEntity(_state.Audio);

        _adminAudioPanel.SetAudioStream(audioEntity);
        _adminAudioPanel.UpdateGlobalToggled(_state.Global);
        _adminAudioPanel.UpdatePlayersContainer(_state.Players, _state.SelectedPlayers);
        _adminAudioPanel.UpdatePlayingState(_state.Playing);
        _adminAudioPanel.UpdateQueue(_state.Queue);
        _adminAudioPanel.UpdateVolume(_state.Volume);

        if (_entity.TryGetComponent<AudioComponent>(audioEntity, out var audio))
        {
            _adminAudioPanel.UpdateCurrentTrackLabel(audio.FileName);
        }
        else
        {
            _adminAudioPanel.UpdateCurrentTrackLabel(Loc.GetString("admin-audio-panel-track-name-nothing-playing"));
        }
    }
}

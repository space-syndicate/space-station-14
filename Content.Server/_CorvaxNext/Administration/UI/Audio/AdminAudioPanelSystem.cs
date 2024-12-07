using System.Linq;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._CorvaxNext.Administration.UI.Audio;

public sealed partial class AdminAudioPanelSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private (EntityUid Entity, AudioComponent Audio)? _audioStream;
    private List<ICommonSession> _selectedPlayers = new();

    public Action? AudioUpdated;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Playing)
            return;

        if (_audioStream is { } audioStream)
        {
            if (Exists(audioStream.Entity) &&
                (audioStream.Audio.State == AudioState.Playing || audioStream.Audio.State == AudioState.Paused))
                return;
            else
            {
                _audioStream = null;
                Queue.TryDequeue(out _);
                AudioUpdated?.Invoke();
            }
        }

        if (CurrentTrack != null)
        {
            _audioStream = _audio.PlayGlobal(CurrentTrack, Global ? Filter.Broadcast() : Filter.Empty().AddPlayers(_selectedPlayers), Global, AudioParams);
            AudioUpdated?.Invoke();
        }
    }

    private void SetStreamState(AudioState state)
    {
        if (_audioStream != null)
            _audio.SetState(_audioStream.Value.Entity, state, true, _audioStream.Value.Audio);
        AudioUpdated?.Invoke();
    }

    /// <summary>
    /// Stops sound and starts its over with current params with playback position of stopped sound.
    /// Used, for example, when need to update selected players.
    /// </summary>
    private void RecreateSound()
    {
        if (!Playing)
            return;

        if (_audioStream is not { } audioStream)
            return;

        Playing = false;

        var playback = (float)((audioStream.Audio.PauseTime ?? _timing.CurTime) - audioStream.Audio.AudioStart).TotalSeconds;

        _audio.Stop(audioStream.Entity, audioStream.Audio);

        _audioStream = _audio.PlayGlobal(CurrentTrack, Global ? Filter.Broadcast() : Filter.Empty().AddPlayers(_selectedPlayers), Global, AudioParams);
        _audio.SetPlaybackPosition(_audioStream, playback);

        Playing = true;
        AudioUpdated?.Invoke();
    }

    #region Public API
    public readonly Queue<string> Queue = new();
    public string? CurrentTrack
    {
        get
        {
            if (Queue.TryPeek(out var track))
                return track;
            return null;
        }
    }
    public AudioParams AudioParams { get; private set; } = AudioParams.Default;
    public bool Playing { get; private set; } = false;
    public bool Global { get; private set; } = true;
    public HashSet<Guid> SelectedPlayers => _selectedPlayers.Select(player => player.UserId.UserId).ToHashSet();
    public EntityUid AudioEntity => _audioStream?.Entity ?? EntityUid.Invalid;

    public void AddToQueue(string filename)
    {
        Queue.Enqueue(filename);
        AudioUpdated?.Invoke();
    }

    public bool Play()
    {
        if (_audioStream != null && _audioStream.Value.Audio.State == AudioState.Paused)
        {
            return Resume();
        }

        Playing = true;
        AudioUpdated?.Invoke();
        return Playing;
    }

    public void Pause()
    {
        if (_audioStream != null && _audioStream.Value.Audio.State == AudioState.Playing)
            SetStreamState(AudioState.Paused);

        Playing = false;
        AudioUpdated?.Invoke();
    }

    public bool Resume()
    {
        if (_audioStream != null && _audioStream.Value.Audio.State == AudioState.Paused)
            SetStreamState(AudioState.Playing);

        Playing = true;
        AudioUpdated?.Invoke();
        return Playing;
    }

    public bool Stop()
    {
        if (_audioStream != null && _audioStream.Value.Audio.State != AudioState.Stopped)
        {
            _audio.Stop(_audioStream.Value.Entity, _audioStream.Value.Audio);
            _audioStream = null;
            Queue.TryDequeue(out _);
        }

        Playing = false;
        AudioUpdated?.Invoke();
        return !Playing;
    }

    public void SetVolume(float volume)
    {
        AudioParams = AudioParams.WithVolume(volume);
        if (_audioStream != null)
            _audio.SetVolume(_audioStream.Value.Entity, volume, _audioStream.Value.Audio);
        AudioUpdated?.Invoke();
    }

    public void SelectPlayer(Guid player)
    {
        if (SelectedPlayers.Contains(player))
            return;

        var session = _playerManager.NetworkedSessions.FirstOrDefault(session => session.UserId.UserId == player);

        if (session == null)
            return;

        _selectedPlayers.Add(session);
        RecreateSound();
        AudioUpdated?.Invoke();
    }

    public void UnselectPlayer(Guid player)
    {
        if (!SelectedPlayers.Contains(player))
            return;

        var session = _playerManager.NetworkedSessions.FirstOrDefault(session => session.UserId.UserId == player);

        if (session == null)
            return;

        _selectedPlayers.Remove(session);
        RecreateSound();
        AudioUpdated?.Invoke();
    }

    public void SetPlaybackPosition(float position)
    {
        if (CurrentTrack != null && _audioStream is { } audioStream)
        {
            _audio.SetPlaybackPosition(audioStream, position);
        }
        AudioUpdated?.Invoke();
    }

    public void SetGlobal(bool global)
    {
        Global = global;
        RecreateSound();
        AudioUpdated?.Invoke();
    }

    #endregion
}

using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Corvax.AdminMusic;
using Content.Shared.Corvax.CCCVars;
using Robust.Client;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.AdminMusic;

/// <summary>
///     Принимает треки админской музыки с сервера, проигрывает их локально, отправляет загруженные
///     администратором файлы и отвечает за предпрослушивание.
/// </summary>
/// <remarks>
///     Полученные файлы хранятся только в памяти (декодированный <see cref="AudioStream"/>)
///     и никогда не пишутся на диск, поэтому после выхода из игры от них ничего не остаётся.
/// </remarks>
public sealed partial class AdminMusicSystem : EntitySystem
{
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private IBaseClient _client = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITransferManager _transfer = default!;
    [Dependency] private AudioSystem _audio = default!;

    /// <summary>
    ///     Guid + int32 длина данных.
    /// </summary>
    private const int HeaderSize = 20;

    private static readonly TimeSpan DisposeCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Загруженные треки: id -> декодированный поток.
    /// </summary>
    private readonly Dictionary<Guid, AudioStream> _loaded = new();

    /// <summary>
    ///     Треки, которые нужно освободить, как только перестанут существовать аудио-сущности с ними.
    /// </summary>
    private readonly Dictionary<ResPath, AudioStream> _pendingDispose = new();

    /// <summary>
    ///     Треки, удалённые сервером. Храним до отключения, чтобы не загрузить трек снова,
    ///     если повторная передача закончится уже после удаления.
    /// </summary>
    private readonly HashSet<Guid> _disposedIds = new();

    /// <summary>
    ///     Последнее состояние воспроизведения каждого трека от сервера.
    /// </summary>
    private readonly Dictionary<Guid, AdminMusicPlaybackEvent> _playback = new();

    /// <summary>
    ///     Локальные аудио-сущности играющих треков.
    /// </summary>
    private readonly Dictionary<Guid, EntityUid> _playing = new();

    private bool _enabled = true;
    private float _volume = 1f;

    private TimeSpan _nextDisposeCheck;
    private int _maxSizeBytes;

    private EntityUid? _previewEntity;
    private ResPath? _previewPath;
    private TimeSpan _previewEnd;

    public bool IsPreviewing => _previewEntity != null;

    public event Action? PreviewStopped;

    public override void Initialize()
    {
        base.Initialize();

        try
        {
            _transfer.RegisterTransferMessage(AdminMusicConstants.UploadTransferKey);
            _transfer.RegisterTransferMessage(AdminMusicConstants.DownloadTransferKey,
                ev => EntityManager.System<AdminMusicSystem>().OnDownloadReceived(ev));
        }
        catch (InvalidOperationException)
        {
            // Уже зарегистрировано предыдущим экземпляром системы.
        }

        SubscribeNetworkEvent<AdminMusicTrackDisposedEvent>(OnTrackDisposed);
        SubscribeNetworkEvent<AdminMusicPlaybackEvent>(OnPlayback);

        _client.RunLevelChanged += OnRunLevelChanged;

        Subs.CVar(_cfg, CCCVars.AdminMusicMaxSizeMb, v => _maxSizeBytes = (int) (v * 1024 * 1024), true);
        Subs.CVar(_cfg, CCVars.AdminSoundsEnabled, OnEnabledChanged, true);
        Subs.CVar(_cfg, CCCVars.AdminMusicVolume, OnVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _client.RunLevelChanged -= OnRunLevelChanged;
        StopPreview();
        ReleaseAll();
        DisposePending(force: true);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_previewEntity != null && (_timing.RealTime >= _previewEnd || !Exists(_previewEntity)))
            StopPreview();

        if (_pendingDispose.Count == 0 || _timing.RealTime < _nextDisposeCheck)
            return;

        _nextDisposeCheck = _timing.RealTime + DisposeCheckInterval;
        DisposePending(force: false);
    }

    #region Download

    private async void OnDownloadReceived(TransferReceivedEvent ev)
    {
        await using var stream = ev.DataStream;

        var trackId = Guid.Empty;
        try
        {
            var header = new byte[HeaderSize];
            await stream.ReadExactlyAsync(header);

            trackId = new Guid(header.AsSpan(0, 16));
            var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16, 4));

            if (length <= 0 || length > _maxSizeBytes)
            {
                Log.Warning($"Received admin music {trackId} with invalid size {length}");
                RaiseNetworkEvent(new AdminMusicDownloadedEvent(trackId, false));
                return;
            }

            var data = new byte[length];
            await stream.ReadExactlyAsync(data);

            // Трек уже удалён сервером, пока мы его качали.
            if (_disposedIds.Contains(trackId))
                return;

            // Повторная отправка: файл уже есть, просто подтверждаем.
            if (!_loaded.ContainsKey(trackId))
                LoadTrack(trackId, data);

            RaiseNetworkEvent(new AdminMusicDownloadedEvent(trackId, true));
        }
        catch (Exception e)
        {
            Log.Error($"Failed to receive admin music {trackId}: {e}");

            if (trackId != Guid.Empty)
                RaiseNetworkEvent(new AdminMusicDownloadedEvent(trackId, false));
        }
    }

    private void LoadTrack(Guid trackId, byte[] data)
    {
        var path = AdminMusicConstants.GetTrackPath(trackId);
        var audioStream = _audioManager.LoadAudioOggVorbis(new MemoryStream(data, false), path.ToString());

        // В кэш ресурсов поток не кладём: трек играется локально прямо из потока,
        // а удалить AudioResource из кэша движок не позволяет.
        _loaded[trackId] = audioStream;
    }

    private void OnTrackDisposed(AdminMusicTrackDisposedEvent ev)
    {
        StopTrack(ev.TrackId);
        _playback.Remove(ev.TrackId);
        _disposedIds.Add(ev.TrackId);

        if (!_loaded.Remove(ev.TrackId, out var stream))
            return;

        _pendingDispose[AdminMusicConstants.GetTrackPath(ev.TrackId)] = stream;
    }

    #endregion

    #region Playback

    private void OnPlayback(AdminMusicPlaybackEvent ev)
    {
        var volumeOnly = _playback.TryGetValue(ev.TrackId, out var old)
                         && old.AudioStart == ev.AudioStart
                         && old.PauseTime == ev.PauseTime;

        _playback[ev.TrackId] = ev;

        // Изменилась только громкость — не перезапускаем трек.
        if (volumeOnly && _playing.TryGetValue(ev.TrackId, out var uid) && Exists(uid))
        {
            _audio.SetVolume(uid, GetVolume(ev));
            return;
        }

        RestartTrack(ev.TrackId);
    }

    /// <summary>
    ///     Запускает трек заново с позиции, рассчитанной по состоянию от сервера.
    /// </summary>
    private void RestartTrack(Guid trackId)
    {
        StopTrack(trackId);

        if (!_enabled
            || !_playback.TryGetValue(trackId, out var playback)
            || !_loaded.TryGetValue(trackId, out var stream))
            return;

        var position = (float) ((playback.PauseTime ?? _timing.CurTime) - playback.AudioStart).TotalSeconds;
        position = Math.Max(0f, position);
        if (position >= stream.Length.TotalSeconds)
            return;

        var audioParams = AudioParams.Default
            .WithVolume(GetVolume(playback))
            .WithPlayOffset(position);

        var specifier = new ResolvedPathSpecifier(AdminMusicConstants.GetTrackPath(trackId));
        if (_audio.PlayGlobal(stream, specifier, audioParams) is not { } audio)
            return;

        if (playback.PauseTime != null)
            _audio.SetState(audio.Entity, AudioState.Paused, component: audio.Component);

        _playing[trackId] = audio.Entity;
    }

    private void StopTrack(Guid trackId)
    {
        if (_playing.Remove(trackId, out var uid))
            _audio.Stop(uid);
    }

    /// <summary>
    ///     Громкость трека с учётом настройки громкости админской музыки.
    /// </summary>
    private float GetVolume(AdminMusicPlaybackEvent playback)
    {
        return playback.Volume + SharedAudioSystem.GainToVolume(_volume);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        foreach (var trackId in _playback.Keys.ToArray())
        {
            if (enabled)
                RestartTrack(trackId);
            else
                StopTrack(trackId);
        }
    }

    private void OnVolumeChanged(float volume)
    {
        _volume = volume;

        foreach (var (trackId, uid) in _playing)
        {
            if (Exists(uid) && _playback.TryGetValue(trackId, out var playback))
                _audio.SetVolume(uid, GetVolume(playback));
        }
    }

    #endregion

    #region Upload

    /// <summary>
    ///     Отправляет файл трека на сервер. Вызывается после того, как сервер одобрил запрос.
    /// </summary>
    public async void Upload(Guid trackId, byte[] data)
    {
        if (_net.ServerChannel is not { } channel)
            return;

        try
        {
            await using var stream = _transfer.StartTransfer(channel, AdminMusicConstants.UploadTransferKey);

            var header = new byte[HeaderSize];
            trackId.TryWriteBytes(header.AsSpan(0, 16));
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), data.Length);

            await stream.WriteAsync(header);
            await stream.WriteAsync(data);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to upload admin music {trackId}: {e}");
        }
    }

    #endregion

    #region Preview

    /// <summary>
    ///     Проигрывает трек локально, только для себя.
    /// </summary>
    public bool StartPreview(byte[] data, float start, float end, float volume)
    {
        StopPreview();

        AudioStream audioStream;
        var path = AdminMusicConstants.Prefix / "Preview" / $"{Guid.NewGuid()}.ogg";

        try
        {
            audioStream = _audioManager.LoadAudioOggVorbis(new MemoryStream(data, false), path.ToString());
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load admin music preview: {e}");
            return false;
        }

        var length = (float) audioStream.Length.TotalSeconds;
        start = Math.Clamp(start, 0f, length);
        if (end <= start || end > length)
            end = length;

        var audioParams = AudioParams.Default
            .WithVolume(volume)
            .WithPlayOffset(start);

        var playing = _audio.PlayGlobal(audioStream, new ResolvedPathSpecifier(path), audioParams);
        if (playing == null)
        {
            audioStream.Dispose();
            return false;
        }

        _previewEntity = playing.Value.Entity;
        _previewPath = path;
        _previewEnd = _timing.RealTime + TimeSpan.FromSeconds(end - start);
        _pendingDispose[path] = audioStream;

        return true;
    }

    public void StopPreview()
    {
        if (_previewEntity == null)
            return;

        _audio.Stop(_previewEntity);
        _previewEntity = null;
        _previewPath = null;

        PreviewStopped?.Invoke();
    }

    #endregion

    #region Cleanup

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
    {
        if (e.NewLevel != ClientRunLevel.Initialize)
            return;

        // Отключились от сервера: все полученные треки больше не нужны.
        StopPreview();
        ReleaseAll();
    }

    private void ReleaseAll()
    {
        foreach (var trackId in _playing.Keys.ToArray())
        {
            StopTrack(trackId);
        }

        _playback.Clear();

        foreach (var (trackId, stream) in _loaded)
        {
            _pendingDispose[AdminMusicConstants.GetTrackPath(trackId)] = stream;
        }

        _loaded.Clear();
        _disposedIds.Clear();
    }

    /// <summary>
    ///     Освобождает буферы треков. Буфер нельзя удалять, пока его использует источник звука,
    ///     поэтому ждём удаления всех аудио-сущностей с этим файлом.
    /// </summary>
    private void DisposePending(bool force)
    {
        if (_pendingDispose.Count == 0)
            return;

        var inUse = new HashSet<string>();
        if (!force)
        {
            var query = AllEntityQuery<AudioComponent>();
            while (query.MoveNext(out _, out var audio))
            {
                inUse.Add(audio.FileName);
            }
        }

        var toRemove = new List<ResPath>();
        foreach (var (path, stream) in _pendingDispose)
        {
            if (path == _previewPath || inUse.Contains(path.ToString()))
                continue;

            stream.Dispose();
            toRemove.Add(path);
        }

        foreach (var path in toRemove)
        {
            _pendingDispose.Remove(path);
        }
    }

    #endregion
}

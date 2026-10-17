using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Corvax.AdminMusic;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Station.Systems;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Corvax.AdminMusic;

/// <summary>
///     Принимает .ogg файлы от администраторов, рассылает их выбранным игрокам и воспроизводит.
/// </summary>
public sealed partial class AdminMusicSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private ITransferManager _transfer = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StationSystem _station = default!;

    /// <summary>
    ///     Сколько ждём начала загрузки файла после одобрения запроса.
    /// </summary>
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Сколько максимум может длиться сама загрузка файла от администратора.
    /// </summary>
    private static readonly TimeSpan UploadReadTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Сколько максимум может длиться отправка файла одному игроку, прежде чем считать её зависшей.
    /// </summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Guid + int32 длина данных.
    /// </summary>
    private const int HeaderSize = 20;

    private readonly MemoryContentRoot _contentRoot = new();
    private readonly Dictionary<Guid, AdminMusicTrack> _tracks = new();
    private readonly HashSet<AdminMusicEui> _euis = new();

    private int _maxSizeBytes;
    private int _maxActive;
    private float _minVolume;
    private float _maxVolume;
    private TimeSpan _downloadTimeout;
    private int _downloadAttempts;

    public override void Initialize()
    {
        base.Initialize();

        _res.AddRoot(AdminMusicConstants.Prefix, _contentRoot);

        try
        {
            _transfer.RegisterTransferMessage(AdminMusicConstants.UploadTransferKey,
                ev => EntityManager.System<AdminMusicSystem>().OnUploadReceived(ev));
            _transfer.RegisterTransferMessage(AdminMusicConstants.DownloadTransferKey);
        }
        catch (InvalidOperationException)
        {
            // Уже зарегистрировано предыдущим экземпляром системы.
        }

        SubscribeNetworkEvent<AdminMusicDownloadedEvent>(OnDownloaded);
        SubscribeLocalEvent<AdminMusicComponent, ComponentShutdown>(OnAudioShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        Subs.CVar(_cfg, CCCVars.AdminMusicMaxSizeMb, v => _maxSizeBytes = (int) (v * 1024 * 1024), true);
        Subs.CVar(_cfg, CCCVars.AdminMusicMaxActive, v => _maxActive = v, true);
        Subs.CVar(_cfg, CCCVars.AdminMusicMinVolume, v => _minVolume = v, true);
        Subs.CVar(_cfg, CCCVars.AdminMusicMaxVolume, v => _maxVolume = v, true);
        Subs.CVar(_cfg, CCCVars.AdminMusicDownloadTimeout, v => _downloadTimeout = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_cfg, CCCVars.AdminMusicDownloadAttempts, v => _downloadAttempts = Math.Max(1, v), true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _contentRoot.Clear();
        _tracks.Clear();
    }

    #region EUI

    public void RegisterEui(AdminMusicEui eui)
    {
        _euis.Add(eui);
    }

    public void UnregisterEui(AdminMusicEui eui)
    {
        _euis.Remove(eui);
    }

    private void DirtyEuis()
    {
        foreach (var eui in _euis)
        {
            eui.StateDirty();
        }
    }

    private void SendError(ICommonSession admin, string message, Guid? trackId = null)
    {
        foreach (var eui in _euis)
        {
            if (eui.Player == admin)
                eui.SendMessage(new AdminMusicEuiMsg.Error { Message = message, TrackId = trackId });
        }
    }

    public AdminMusicEuiState GetEuiState()
    {
        var state = new AdminMusicEuiState();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            var data = new AdminMusicPlayerData
            {
                UserId = session.UserId,
                Username = session.Name,
            };

            if (session.AttachedEntity is { } ent && !TerminatingOrDeleted(ent))
            {
                var xform = Transform(ent);
                data.CharacterName = Name(ent);
                data.Grid = GetNetEntity(xform.GridUid);
                data.Map = GetNetEntity(xform.MapUid);
                data.Station = GetNetEntity(_station.GetOwningStation(ent, xform));
            }

            state.Players.Add(data);
        }

        var gridQuery = EntityQueryEnumerator<MapGridComponent, MetaDataComponent>();
        while (gridQuery.MoveNext(out var uid, out _, out var meta))
        {
            state.Objects.Add(new AdminMusicObjectData
            {
                Entity = GetNetEntity(uid),
                Name = meta.EntityName,
                Type = AdminMusicObjectType.Grid,
            });
        }

        var mapQuery = EntityQueryEnumerator<MapComponent, MetaDataComponent>();
        while (mapQuery.MoveNext(out var uid, out _, out var meta))
        {
            state.Objects.Add(new AdminMusicObjectData
            {
                Entity = GetNetEntity(uid),
                Name = meta.EntityName,
                Type = AdminMusicObjectType.Map,
            });
        }

        foreach (var station in _station.GetStations())
        {
            state.Objects.Add(new AdminMusicObjectData
            {
                Entity = GetNetEntity(station),
                Name = Name(station),
                Type = AdminMusicObjectType.Station,
            });
        }

        foreach (var track in _tracks.Values)
        {
            var data = new AdminMusicTrackData
            {
                Id = track.Id,
                Title = track.Title,
                AdminName = track.AdminName,
                Status = track.Status,
                Length = track.Length,
                Start = track.Start,
                End = track.End,
                Volume = track.Volume,
                Listeners = track.Recipients.Values.Count(r => r.Status == RecipientStatus.Received),
                Pending = track.Recipients.Values.Count(r => r.Status == RecipientStatus.Pending),
                Failed = track.Recipients.Values.Count(r => r.Status == RecipientStatus.Failed),
            };

            if (TryComp<AudioComponent>(track.Audio, out var audio))
            {
                data.AudioStart = audio.AudioStart;
                data.PauseTime = audio.PauseTime;
            }

            state.Tracks.Add(data);
        }

        return state;
    }

    #endregion

    #region Requests

    public void RequestPlay(AdminMusicEui eui, AdminMusicEuiMsg.RequestPlay msg)
    {
        var admin = eui.Player;

        if (_tracks.Count >= _maxActive)
        {
            SendError(admin, Loc.GetString("admin-music-error-max-active", ("max", _maxActive)), msg.TrackId);
            return;
        }

        if (msg.Size <= 0 || msg.Size > _maxSizeBytes)
        {
            SendError(admin, Loc.GetString("admin-music-error-too-large", ("max", _maxSizeBytes / 1024 / 1024)), msg.TrackId);
            return;
        }

        if (msg.TrackId == Guid.Empty
            || _tracks.ContainsKey(msg.TrackId)
            || !float.IsFinite(msg.Volume)
            || !float.IsFinite(msg.Start)
            || !float.IsFinite(msg.End))
        {
            SendError(admin, Loc.GetString("admin-music-error-generic"), msg.TrackId);
            return;
        }

        var recipients = new Dictionary<NetUserId, AdminMusicRecipient>();
        foreach (var userId in msg.Recipients)
        {
            if (_playerManager.TryGetSessionById(userId, out var session) && session.Status == SessionStatus.InGame)
                recipients[userId] = new AdminMusicRecipient(session);
        }

        if (recipients.Count == 0)
        {
            SendError(admin, Loc.GetString("admin-music-error-no-recipients"), msg.TrackId);
            return;
        }

        var title = msg.Title.Trim();
        if (title.Length > AdminMusicConstants.MaxTitleLength)
            title = title[..AdminMusicConstants.MaxTitleLength];
        if (string.IsNullOrEmpty(title))
            title = msg.TrackId.ToString();

        var track = new AdminMusicTrack
        {
            Id = msg.TrackId,
            Title = title,
            Admin = admin,
            AdminName = admin.Name,
            Size = msg.Size,
            Volume = Math.Clamp(msg.Volume, _minVolume, _maxVolume),
            Start = Math.Max(0f, msg.Start),
            End = msg.End,
            StartPaused = msg.StartPaused,
            Status = AdminMusicTrackStatus.Uploading,
            StatusChanged = _timing.RealTime,
            Recipients = recipients,
        };

        _tracks.Add(track.Id, track);
        eui.SendMessage(new AdminMusicEuiMsg.UploadPermitted { TrackId = track.Id });
        DirtyEuis();
    }

    public void SetPaused(ICommonSession admin, Guid trackId, bool paused)
    {
        if (!_tracks.TryGetValue(trackId, out var track) || track.Audio is not { } audio)
            return;

        _audio.SetState(audio, paused ? AudioState.Paused : AudioState.Playing);
        track.Status = paused ? AdminMusicTrackStatus.Paused : AdminMusicTrackStatus.Playing;
        SendPlayback(track);

        _adminLog.Add(LogType.AdminCommands, LogImpact.Low,
            $"{admin:actor} {(paused ? "paused" : "resumed")} admin music \"{track.Title}\" ({track.Id})");

        DirtyEuis();
    }

    public void Seek(ICommonSession admin, Guid trackId, float position)
    {
        if (!float.IsFinite(position) || !_tracks.TryGetValue(trackId, out var track) || track.Audio is not { } audio)
            return;

        position = Math.Clamp(position, 0f, Math.Max(0f, track.End - 0.1f));
        _audio.SetPlaybackPosition(audio, position);
        SendPlayback(track);

        _adminLog.Add(LogType.AdminCommands, LogImpact.Low,
            $"{admin:actor} moved admin music \"{track.Title}\" ({track.Id}) to {position:F1}s");

        DirtyEuis();
    }

    public void SetVolume(ICommonSession admin, Guid trackId, float volume)
    {
        if (!float.IsFinite(volume) || !_tracks.TryGetValue(trackId, out var track))
            return;

        track.Volume = Math.Clamp(volume, _minVolume, _maxVolume);

        if (track.Audio is { } audio)
        {
            _audio.SetVolume(audio, track.Volume);
            SendPlayback(track);
        }

        _adminLog.Add(LogType.AdminCommands, LogImpact.Low,
            $"{admin:actor} set volume of admin music \"{track.Title}\" ({track.Id}) to {track.Volume:F1}dB");

        DirtyEuis();
    }

    public void StopTrack(ICommonSession admin, Guid trackId)
    {
        if (!_tracks.TryGetValue(trackId, out var track))
            return;

        _adminLog.Add(LogType.AdminCommands, LogImpact.High,
            $"{admin:actor} removed admin music \"{track.Title}\" ({track.Id}) started by {track.AdminName}");

        RemoveTrack(track);
    }

    #endregion

    #region Transfer

    private async void OnUploadReceived(TransferReceivedEvent ev)
    {
        await using var stream = ev.DataStream;

        if (!_playerManager.TryGetSessionByChannel(ev.Channel, out var session)
            || !_adminManager.HasAdminFlag(session, AdminFlags.Fun))
        {
            Log.Warning($"Rejected admin music upload from unauthorized channel {ev.Channel}");
            return;
        }

        AdminMusicTrack? track = null;
        try
        {
            var header = new byte[HeaderSize];
            await stream.ReadExactlyAsync(header);

            var trackId = new Guid(header.AsSpan(0, 16));
            var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16, 4));

            if (!_tracks.TryGetValue(trackId, out track)
                || track.Admin != session
                || track.Status != AdminMusicTrackStatus.Uploading
                || length != track.Size
                || length > _maxSizeBytes)
            {
                Log.Warning($"Rejected unexpected admin music upload {trackId} from {session}");
                return;
            }

            // Не даём загрузить файл с тем же id повторно.
            track.Status = AdminMusicTrackStatus.Distributing;
            track.StatusChanged = _timing.RealTime;

            var data = new byte[length];
            await stream.ReadExactlyAsync(data);

            // Трек могли удалить, пока мы читали поток.
            if (!_tracks.ContainsKey(trackId))
                return;

            OnUploadFinished(track, data);
        }
        catch (Exception e)
        {
            Log.Error($"Error while receiving admin music upload from {session}: {e}");

            if (track != null && _tracks.ContainsKey(track.Id))
            {
                SendError(session, Loc.GetString("admin-music-error-upload"), track.Id);
                RemoveTrack(track);
            }
        }
    }

    private void OnUploadFinished(AdminMusicTrack track, byte[] data)
    {
        var path = AdminMusicConstants.GetTrackPath(track.Id);

        if (!AdminMusicConstants.IsOgg(data))
        {
            SendError(track.Admin, Loc.GetString("admin-music-error-invalid-file"), track.Id);
            RemoveTrack(track);
            return;
        }

        // В content root путь относительный: root уже примонтирован по AdminMusicConstants.Prefix.
        _contentRoot.AddOrUpdateFile(AdminMusicConstants.GetTrackFileName(track.Id), data);

        try
        {
            track.Length = (float) _audio.GetAudioLength(new ResolvedPathSpecifier(path)).TotalSeconds;
        }
        catch (Exception e)
        {
            Log.Warning($"Admin music {track.Id} from {track.Admin} is not a valid audio file: {e.Message}");
            SendError(track.Admin, Loc.GetString("admin-music-error-invalid-file"), track.Id);
            RemoveTrack(track);
            return;
        }

        track.Start = Math.Clamp(track.Start, 0f, track.Length);
        if (track.End <= track.Start || track.End > track.Length)
            track.End = track.Length;

        if (track.End - track.Start < 0.5f)
        {
            SendError(track.Admin, Loc.GetString("admin-music-error-too-short"), track.Id);
            RemoveTrack(track);
            return;
        }

        track.Data = data;
        track.StatusChanged = _timing.RealTime;

        _adminLog.Add(LogType.AdminCommands, LogImpact.High,
            $"{track.Admin:actor} played admin music \"{track.Title}\" ({track.Id}, {data.Length / 1024}KB, {track.Start:F1}s-{track.End:F1}s, {track.Volume:F1}dB, paused: {track.StartPaused}) for {track.Recipients.Count} players: {string.Join(", ", track.Recipients.Values.Select(r => r.Session.Name))}");

        foreach (var recipient in track.Recipients.Values)
        {
            SendToRecipient(track, recipient);
        }

        DirtyEuis();
    }

    private async void SendToRecipient(AdminMusicTrack track, AdminMusicRecipient recipient)
    {
        if (track.Data is not { } data)
            return;

        recipient.Attempts++;
        recipient.LastSent = _timing.RealTime;

        if (recipient.Session.Status == SessionStatus.Disconnected)
        {
            recipient.Status = RecipientStatus.Failed;
            return;
        }

        recipient.Sending = true;
        try
        {
            await using var stream = _transfer.StartTransfer(recipient.Session.Channel, AdminMusicConstants.DownloadTransferKey);

            var header = new byte[HeaderSize];
            track.Id.TryWriteBytes(header.AsSpan(0, 16));
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), data.Length);

            await stream.WriteAsync(header);
            await stream.WriteAsync(data);
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to send admin music {track.Id} to {recipient.Session}: {e.Message}");
        }
        finally
        {
            // Таймаут подтверждения считаем с момента окончания отправки.
            recipient.Sending = false;
            recipient.LastSent = _timing.RealTime;
        }
    }

    private void OnDownloaded(AdminMusicDownloadedEvent ev, EntitySessionEventArgs args)
    {
        if (!_tracks.TryGetValue(ev.TrackId, out var track)
            || !track.Recipients.TryGetValue(args.SenderSession.UserId, out var recipient)
            || recipient.Status == RecipientStatus.Received)
            return;

        // Успешная загрузка засчитывается, даже если игрока уже пометили Failed по таймауту.
        if (!ev.Success && recipient.Status == RecipientStatus.Failed)
            return;

        if (!ev.Success)
        {
            if (recipient.Attempts < _downloadAttempts)
                SendToRecipient(track, recipient);
            else
                recipient.Status = RecipientStatus.Failed;

            DirtyEuis();
            return;
        }

        recipient.Status = RecipientStatus.Received;

        // Трек уже играет — опоздавший игрок подключается с текущей позиции.
        if (GetPlaybackEvent(track) is { } playback)
            RaiseNetworkEvent(playback, recipient.Session);

        DirtyEuis();
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_tracks.Count == 0)
            return;

        var now = _timing.RealTime;

        foreach (var track in _tracks.Values.ToArray())
        {
            switch (track.Status)
            {
                case AdminMusicTrackStatus.Uploading:
                    if (now - track.StatusChanged > UploadTimeout)
                    {
                        SendError(track.Admin, Loc.GetString("admin-music-error-upload"), track.Id);
                        RemoveTrack(track);
                    }

                    break;

                case AdminMusicTrackStatus.Distributing:
                    if (track.Data == null)
                    {
                        if (now - track.StatusChanged > UploadReadTimeout)
                        {
                            SendError(track.Admin, Loc.GetString("admin-music-error-upload"), track.Id);
                            RemoveTrack(track);
                        }

                        break;
                    }

                    UpdateRecipients(track, now);

                    var anyPending = track.Recipients.Values.Any(r => r.Status == RecipientStatus.Pending);
                    var anyReceived = track.Recipients.Values.Any(r => r.Status == RecipientStatus.Received);

                    if (!anyPending && !anyReceived)
                    {
                        SendError(track.Admin, Loc.GetString("admin-music-error-download-failed"), track.Id);
                        RemoveTrack(track);
                        break;
                    }

                    // Стартуем, когда все получили файл или истёк таймаут первой попытки.
                    // Те, кто ещё качает, подключатся позже.
                    if (!anyPending || anyReceived && now - track.StatusChanged > _downloadTimeout)
                        StartPlayback(track);

                    break;

                case AdminMusicTrackStatus.Playing:
                case AdminMusicTrackStatus.Paused:
                    UpdateRecipients(track, now);

                    if (!TryComp<AudioComponent>(track.Audio, out var audio))
                        break;

                    var position = ((audio.PauseTime ?? _timing.CurTime) - audio.AudioStart).TotalSeconds;
                    if (position >= track.End)
                        RemoveTrack(track);

                    break;
            }
        }
    }

    private void UpdateRecipients(AdminMusicTrack track, TimeSpan now)
    {
        var changed = false;

        foreach (var recipient in track.Recipients.Values)
        {
            if (recipient.Status != RecipientStatus.Pending)
                continue;

            // Пока файл ещё отправляется, повторно его не шлём.
            if (recipient.Sending)
            {
                if (now - recipient.LastSent > SendTimeout)
                {
                    recipient.Status = RecipientStatus.Failed;
                    changed = true;
                }

                continue;
            }

            if (now - recipient.LastSent <= _downloadTimeout)
                continue;

            if (recipient.Attempts < _downloadAttempts)
            {
                SendToRecipient(track, recipient);
                continue;
            }

            recipient.Status = RecipientStatus.Failed;
            changed = true;
        }

        if (changed)
            DirtyEuis();
    }

    private void StartPlayback(AdminMusicTrack track)
    {
        var path = AdminMusicConstants.GetTrackPath(track.Id);
        var audioParams = AudioParams.Default.WithVolume(track.Volume);

        // Клиенты играют его локально по AdminMusicPlaybackEvent, чтобы применять свою громкость.
        if (_audio.PlayGlobal(new ResolvedPathSpecifier(path), Filter.Empty(), false, audioParams) is not { } audio)
        {
            SendError(track.Admin, Loc.GetString("admin-music-error-generic"), track.Id);
            RemoveTrack(track);
            return;
        }

        track.Audio = audio.Entity;
        AddComp<AdminMusicComponent>(audio.Entity).TrackId = track.Id;

        // Позиция сетевого аудио считается от AudioStart, поэтому PlayOffset тут не подходит.
        if (track.Start > 0f)
            _audio.SetPlaybackPosition(audio, track.Start);

        if (track.StartPaused)
        {
            _audio.SetState(audio.Entity, AudioState.Paused, component: audio.Component);
            track.Status = AdminMusicTrackStatus.Paused;
        }
        else
        {
            track.Status = AdminMusicTrackStatus.Playing;
        }

        track.StatusChanged = _timing.RealTime;
        SendPlayback(track);
        DirtyEuis();
    }

    private AdminMusicPlaybackEvent? GetPlaybackEvent(AdminMusicTrack track)
    {
        if (!TryComp<AudioComponent>(track.Audio, out var audio))
            return null;

        return new AdminMusicPlaybackEvent
        {
            TrackId = track.Id,
            AudioStart = audio.AudioStart,
            PauseTime = audio.PauseTime,
            Volume = track.Volume,
        };
    }

    /// <summary>
    ///     Рассылает текущее состояние воспроизведения всем, кто уже получил трек.
    /// </summary>
    private void SendPlayback(AdminMusicTrack track)
    {
        if (GetPlaybackEvent(track) is not { } playback)
            return;

        var filter = Filter.Empty();
        foreach (var recipient in track.Recipients.Values)
        {
            if (recipient.Status == RecipientStatus.Received && recipient.Session.Status == SessionStatus.InGame)
                filter.AddPlayer(recipient.Session);
        }

        if (filter.Count > 0)
            RaiseNetworkEvent(playback, filter);
    }

    #region Cleanup

    private void RemoveTrack(AdminMusicTrack track)
    {
        if (!_tracks.Remove(track.Id))
            return;

        _contentRoot.RemoveFile(AdminMusicConstants.GetTrackFileName(track.Id));
        track.Data = null;

        var filter = Filter.Empty();
        foreach (var recipient in track.Recipients.Values)
        {
            if (recipient.Attempts > 0 && recipient.Session.Status != SessionStatus.Disconnected)
                filter.AddPlayer(recipient.Session);
        }

        if (filter.Count > 0)
            RaiseNetworkEvent(new AdminMusicTrackDisposedEvent(track.Id), filter);

        if (track.Audio is { } audio && !TerminatingOrDeleted(audio))
            QueueDel(audio);

        track.Audio = null;
        DirtyEuis();
    }

    private void OnAudioShutdown(Entity<AdminMusicComponent> ent, ref ComponentShutdown args)
    {
        // Аудио доиграло само или было удалено чем-то другим.
        if (_tracks.TryGetValue(ent.Comp.TrackId, out var track))
            RemoveTrack(track);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var track in _tracks.Values.ToArray())
        {
            RemoveTrack(track);
        }
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
        {
            if (e.NewStatus == SessionStatus.InGame)
                DirtyEuis();

            return;
        }

        foreach (var track in _tracks.Values)
        {
            if (track.Recipients.TryGetValue(e.Session.UserId, out var recipient)
                && recipient.Status == RecipientStatus.Pending)
                recipient.Status = RecipientStatus.Failed;

            if (track.Admin == e.Session && track.Status == AdminMusicTrackStatus.Uploading)
                track.StatusChanged = TimeSpan.Zero; // Удалится на следующем Update.
        }

        DirtyEuis();
    }

    #endregion

    private sealed class AdminMusicTrack
    {
        public Guid Id;
        public string Title = string.Empty;
        public ICommonSession Admin = default!;
        public string AdminName = string.Empty;
        public int Size;
        public byte[]? Data;
        public float Length;
        public float Start;
        public float End;
        public float Volume;
        public bool StartPaused;
        public AdminMusicTrackStatus Status;
        public TimeSpan StatusChanged;
        public Dictionary<NetUserId, AdminMusicRecipient> Recipients = new();
        public EntityUid? Audio;
    }

    private sealed class AdminMusicRecipient(ICommonSession session)
    {
        public readonly ICommonSession Session = session;
        public RecipientStatus Status = RecipientStatus.Pending;
        public int Attempts;

        /// <summary>
        ///     Начало текущей отправки, пока идёт <see cref="Sending"/>, иначе время окончания последней.
        /// </summary>
        public TimeSpan LastSent;

        public bool Sending;
    }

    private enum RecipientStatus : byte
    {
        Pending,
        Received,
        Failed,
    }
}

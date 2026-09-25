using System;
using System.Collections.Generic;
using System.IO;
using Content.Shared.Corvax.Administration.MusicPlayer;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Administration.Logs;
using Content.Shared.Database;

namespace Content.Server.Corvax.Administration.MusicPlayer;

public sealed partial class MusicPlayerSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    private sealed class AdminAudioSession
    {
        public MusicPlayerLoopMode LoopMode { get; set; } = MusicPlayerLoopMode.Off;
        public Guid SessionId { get; }
        public string SessionName { get; set; }
        public string CreatorCkey { get; }
        public List<string> PlaylistTracks { get; }
        public int CurrentTrackIndex { get; set; }
        public Dictionary<string, float> TracksDurationCache { get; set; } = new();

        public string TrackPath => PlaylistTracks.Count > CurrentTrackIndex ? PlaylistTracks[CurrentTrackIndex] : string.Empty;
        public string TrackName => Path.GetFileNameWithoutExtension(TrackPath);

        public bool IsPlaying { get; set; }
        public int Volume { get; set; }
        public TimeSpan StartTime { get; set; }
        public float Duration { get; set; } 
        public float PlayOffset { get; set; } 
        public TimeSpan ExpireTime { get; set; }

        public List<string> Maps { get; }
        public List<string> Players { get; }

        public SharedAdminAudioSession ToShared(float liveOffset)
        {
            return new SharedAdminAudioSession(
                SessionId, SessionName, CreatorCkey, TrackName, 
                IsPlaying, Volume, Duration, liveOffset, 
                new List<string>(PlaylistTracks), CurrentTrackIndex, LoopMode, 
                new List<string>(Maps), new List<string>(Players));
        }

        public AdminAudioSession(
            Guid sessionId, string sessionName, string creatorCkey, 
            List<string> playlistTracks, int volume, 
            float duration, float playOffset,
            List<string> maps, List<string> players)
        {
            SessionId = sessionId;
            SessionName = sessionName;
            CreatorCkey = creatorCkey;
            PlaylistTracks = playlistTracks;
            CurrentTrackIndex = 0;
            IsPlaying = true;
            Volume = volume;
            Duration = duration;
            PlayOffset = playOffset;
            Maps = maps;
            Players = players;
        }

        public bool SwitchToTrack(int index, float newDuration)
        {
            if (index < 0 || index >= PlaylistTracks.Count) return false;
            CurrentTrackIndex = index;
            Duration = newDuration;
            PlayOffset = 0f;
            return true;
        }
    }

    private readonly List<MusicPlayerEui> _activeEuis = new();
    private readonly Dictionary<Guid, AdminAudioSession> _audioSessions = new Dictionary<Guid, AdminAudioSession>();
    private readonly Dictionary<string, string> _availableTracks = new Dictionary<string, string>();
    private string _currentFolderPath = "/Audio/Lobby/";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private readonly Queue<Action> _mainThreadQueue = new();

    public void QueueMethod(Action action)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        List<Action> toRun;
        lock (_mainThreadQueue)
        {
            if (_mainThreadQueue.Count > 0)
            {
                toRun = new List<Action>(_mainThreadQueue);
                _mainThreadQueue.Clear();
                foreach (var action in toRun) action.Invoke();
            }
        }

        var curTime = _timing.RealTime;
        foreach (var session in _audioSessions.Values)
        {
            if (!session.IsPlaying) continue;

            if (curTime >= session.ExpireTime)
            {
                ProcessTrackFinished(session);
                break; 
            }
        }
    }

    private void ProcessTrackFinished(AdminAudioSession session)
    {
        var curTime = _timing.RealTime;

        if (session.LoopMode == MusicPlayerLoopMode.One)
        {
            session.PlayOffset = 0f;
            session.StartTime = curTime;
            
            float loopDuration = session.Duration < 1.5f ? 1.5f : session.Duration;
            session.ExpireTime = curTime + TimeSpan.FromSeconds(loopDuration);
            session.IsPlaying = true; 

            SyncSessionWithAllPlayers(session);
            UpdateAllEuis();
            return;
        }

        bool hasNextTrack = session.CurrentTrackIndex + 1 < session.PlaylistTracks.Count;
        int targetNextIndex = session.CurrentTrackIndex + 1;
        
        if (!hasNextTrack && session.LoopMode == MusicPlayerLoopMode.All)
        {
            targetNextIndex = 0; 
            hasNextTrack = true;
        }

        if (hasNextTrack)
        {
            var nextPath = session.PlaylistTracks[targetNextIndex];
            if (!session.TracksDurationCache.TryGetValue(nextPath, out float nextDuration)) nextDuration = 180f; 
            if (nextDuration < 1.5f) nextDuration = 1.5f;

            session.SwitchToTrack(targetNextIndex, nextDuration);
            session.StartTime = curTime;
            session.ExpireTime = curTime + TimeSpan.FromSeconds(nextDuration);
            session.IsPlaying = true;
            
            SyncSessionWithAllPlayers(session);
            UpdateAllEuis();
        }
        else
        {
            session.IsPlaying = false;
            session.PlayOffset = 0f;

            float firstTrackDuration = 180f;
            if (session.PlaylistTracks.Count > 0)
            {
                var firstTrackPath = session.PlaylistTracks[0];
                if (session.TracksDurationCache.TryGetValue(firstTrackPath, out var cachedDur))
                    firstTrackDuration = cachedDur;
            }

            if (firstTrackDuration < 1.5f) firstTrackDuration = 1.5f;
            session.SwitchToTrack(0, firstTrackDuration);

            foreach (var player in _playerManager.Sessions)
            {
                RaiseNetworkEvent(new AdminMusicClientStopMessage(session.SessionId), player.Channel);
            }

            foreach (var eui in _activeEuis)
            {
                if (eui.Player != null)
                    eui.SendMessage(new Content.Shared.Corvax.Administration.MusicPlayer.AdminMusicClientTrackFinishedMessage(session.SessionId));
            }

            UpdateAllEuis();
        }
    }

    public void RemoveMapFromSession(string actorCkey, Guid sessionId, string mapId)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;

        if (session.Maps.Remove(mapId))
        {
            _adminLogger.Add(LogType.Action, LogImpact.Low, 
                $"[MusicPlayer] Admin '{actorCkey}' removed Map target '{mapId}' from audio session '{session.SessionName}'.");

            SyncSessionWithAllPlayers(session);
            UpdateAllEuis();
        }
    }

    public void RemovePlayerFromSession(string actorCkey, Guid sessionId, string playerCkey)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;

        if (session.Players.Remove(playerCkey))
        {
            _adminLogger.Add(LogType.Action, LogImpact.Low, 
                $"[MusicPlayer] Admin '{actorCkey}' removed Player target '{playerCkey}' from audio session '{session.SessionName}'.");

            foreach (var player in _playerManager.Sessions)
            {
                if (player.Name == playerCkey)
                    RaiseNetworkEvent(new AdminMusicClientStopMessage(session.SessionId), player.Channel);
            }
            SyncSessionWithAllPlayers(session);
            UpdateAllEuis();
        }
    }

    public void NextTrack(Guid sessionId)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;
        if (session.PlaylistTracks.Count <= 1) return;

        int nextIndex = session.CurrentTrackIndex + 1;
        if (nextIndex >= session.PlaylistTracks.Count)
        {
            if (session.LoopMode == MusicPlayerLoopMode.All) nextIndex = 0;
            else return; 
        }

        var nextPath = session.PlaylistTracks[nextIndex];
        if (!session.TracksDurationCache.TryGetValue(nextPath, out float nextDuration)) nextDuration = 180f;
        if (nextDuration < 1.5f) nextDuration = 1.5f;

        session.SwitchToTrack(nextIndex, nextDuration);
        
        var curTime = _timing.RealTime;
        session.StartTime = curTime;
        session.ExpireTime = curTime + TimeSpan.FromSeconds(nextDuration);
        session.IsPlaying = true;

        _adminLogger.Add(LogType.Action, LogImpact.Low, 
            $"[MusicPlayer] Admin '{session.CreatorCkey}' skipped forward to track index {nextIndex + 1} ('{session.TrackName}') in session '{session.SessionName}'.");

        SyncSessionWithAllPlayers(session);
        UpdateAllEuis();
    }

    public void PrevTrack(Guid sessionId)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;
        if (session.PlaylistTracks.Count <= 1) return;

        int prevIndex = session.CurrentTrackIndex - 1;
        if (prevIndex < 0)
        {
            if (session.LoopMode == MusicPlayerLoopMode.All) prevIndex = session.PlaylistTracks.Count - 1;
            else return;
        }

        var prevPath = session.PlaylistTracks[prevIndex];
        if (!session.TracksDurationCache.TryGetValue(prevPath, out float prevDuration)) prevDuration = 180f;
        if (prevDuration < 1.5f) prevDuration = 1.5f;

        session.SwitchToTrack(prevIndex, prevDuration);
        
        var curTime = _timing.RealTime;
        session.StartTime = curTime;
        session.ExpireTime = curTime + TimeSpan.FromSeconds(prevDuration);
        session.IsPlaying = true;

        _adminLogger.Add(LogType.Action, LogImpact.Low, 
            $"[MusicPlayer] Admin '{session.CreatorCkey}' skipped backward to track index {prevIndex + 1} ('{session.TrackName}') in session '{session.SessionName}'.");

        SyncSessionWithAllPlayers(session);
        UpdateAllEuis();
    }

    public void CreateAudioSession(
        string actorCkey, List<string> trackPaths, string sessionName, int startVolume, 
        Dictionary<string, float> trackDurations, List<string>? maps, List<string>? players)
    {
        var validTracks = new List<string>();
        float firstTrackDuration = 180f;

        foreach (var path in trackPaths)
        {
            if (!path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && 
                !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) continue;

            if (!_resourceManager.ContentFileExists(new ResPath(path))) continue;
            validTracks.Add(path);
        }

        if (validTracks.Count == 0) return;

        var firstTrackPath = validTracks[0];
        if (trackDurations.TryGetValue(firstTrackPath, out var clientDuration))
        {
            if (clientDuration >= 0.1f && clientDuration <= 900f) firstTrackDuration = clientDuration;
        }

        var sessionId = Guid.NewGuid();
        var curTime = _timing.RealTime;
        var validMaps = maps ?? new List<string>();
        var validPlayers = players ?? new List<string>();

        var session = new AdminAudioSession(
            sessionId, sessionName, actorCkey, validTracks, startVolume, firstTrackDuration, 0f, validMaps, validPlayers)
        {
            StartTime = curTime,
            ExpireTime = curTime + TimeSpan.FromSeconds(firstTrackDuration),
            IsPlaying = true,
            CurrentTrackIndex = 0,
            TracksDurationCache = trackDurations
        };

        _audioSessions[sessionId] = session;

        var mapsLogged = validMaps.Count > 0 ? string.Join(", ", validMaps) : "None";
        var playersLogged = validPlayers.Count > 0 ? string.Join(", ", validPlayers) : "None";
        _adminLogger.Add(LogType.Action, LogImpact.Medium, 
            $"[MusicPlayer] Admin '{actorCkey}' created audio session '{sessionName}' playing '{session.TrackName}' (Vol: {startVolume}dB). Target Maps: [{mapsLogged}]. Target Players: [{playersLogged}].");

        SyncSessionWithAllPlayers(session);
        UpdateAllEuis();
    }

    public void StopSession(string actorCkey, Guid sessionId)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;

        _adminLogger.Add(LogType.Action, LogImpact.Medium, 
            $"[MusicPlayer] Admin '{actorCkey}' completely stopped and closed audio session '{session.SessionName}'.");

        foreach (var player in _playerManager.Sessions)
        {
            RaiseNetworkEvent(new AdminMusicClientStopMessage(sessionId), player.Channel);
        }

        _audioSessions.Remove(sessionId);
        UpdateAllEuis();
    }

    private bool ShouldPlayerHearSession(ICommonSession player, AdminAudioSession session)
    {
        string playerName = player.Name ?? string.Empty;
        if (string.IsNullOrEmpty(playerName)) return false;
        if (session.Players.Contains(playerName)) return true;

        var playerEntity = player.AttachedEntity;
        if (playerEntity == null) return false;
		
        var playerMapUid = Transform(playerEntity.Value).MapUid;
        if (playerMapUid.HasValue && playerMapUid.Value.IsValid())
        {
            var playerMapNetStr = GetNetEntity(playerMapUid.Value).ToString();
            if (session.Maps.Contains(playerMapNetStr)) return true;
        }
        return false;
    }

    private void SyncSessionWithAllPlayers(AdminAudioSession session)
    {
        foreach (var player in _playerManager.Sessions) SyncPlayerWithSession(player, session);
    }

    private void SyncPlayerWithSession(ICommonSession player, AdminAudioSession session)
    {
        if (!session.IsPlaying)
        {
            RaiseNetworkEvent(new AdminMusicClientStopMessage(session.SessionId), player.Channel);
            return;
        }

        var passedTime = (_timing.RealTime - session.StartTime).TotalSeconds;
        float currentOffset = (float)Math.Round(session.PlayOffset + (float)passedTime, 2);

        if (currentOffset >= session.Duration)
        {
            RaiseNetworkEvent(new AdminMusicClientStopMessage(session.SessionId), player.Channel);
            return;
        }

        if (ShouldPlayerHearSession(player, session))
        {
            RaiseNetworkEvent(new AdminMusicClientPlayMessage(
                session.SessionId, session.TrackPath, currentOffset, (float)session.Volume), player.Channel);
        }
        else
        {
            RaiseNetworkEvent(new AdminMusicClientStopMessage(session.SessionId), player.Channel);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        foreach (var session in _audioSessions.Values) SyncPlayerWithSession(ev.Player, session);
    }
	
    private readonly Dictionary<string, TimeSpan> _playerLastSyncTime = new();

    private void OnParentChanged(ref EntParentChangedMessage ev)
    {
        if (!_playerManager.TryGetSessionByEntity(ev.Entity, out var playerSession)) return;
        if (ev.OldParent == ev.Transform.ParentUid || string.IsNullOrEmpty(playerSession.Name)) return;

        var curTime = _timing.RealTime;
        if (_playerLastSyncTime.TryGetValue(playerSession.Name, out var lastSync))
        {
            if ((curTime - lastSync).TotalMilliseconds < 200) return; 
        }
        _playerLastSyncTime[playerSession.Name] = curTime;

        var targetPlayer = playerSession;
        QueueMethod(() =>
        {
            if (targetPlayer.Status != Robust.Shared.Enums.SessionStatus.InGame) return;
            foreach (var session in _audioSessions.Values) SyncPlayerWithSession(targetPlayer, session);
        });
    }

    public void TogglePauseSession(string actorCkey, Guid sessionId)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;
        var curTime = _timing.RealTime;

        if (session.IsPlaying)
        {
            session.PlayOffset += (float)(curTime - session.StartTime).TotalSeconds;
            if (session.PlayOffset > session.Duration) session.PlayOffset = session.Duration;
        }
        else
        {
            if (session.PlayOffset >= session.Duration)
            {
                session.PlayOffset = 0f;
                session.CurrentTrackIndex = 0;
            }
            session.StartTime = curTime;
            session.ExpireTime = curTime + TimeSpan.FromSeconds(session.Duration - session.PlayOffset);
        }

        session.IsPlaying = !session.IsPlaying;

        _adminLogger.Add(LogType.Action, LogImpact.Low, 
            $"[MusicPlayer] Admin '{actorCkey}' updated playback state on audio session '{session.SessionName}'. Active playing status: {session.IsPlaying} at sequence timeline marker: {session.PlayOffset:F2}s / {session.Duration:F2}s.");

        SyncSessionWithAllPlayers(session);
        UpdateAllEuis();
    }

    public void ChangeSessionVolume(string actorCkey, Guid sessionId, int newVolume)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;
        
        _adminLogger.Add(LogType.Action, LogImpact.Low, 
            $"[MusicPlayer] Admin '{actorCkey}' changed volume on session '{session.SessionName}' to {newVolume} dB.");

        session.Volume = newVolume;
        SyncSessionWithAllPlayers(session);
        UpdateAllEuis();
    }

    public void SeekSession(string actorCkey, Guid sessionId, float targetSeconds)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;
        if (targetSeconds < 0f) targetSeconds = 0f;
        if (targetSeconds > session.Duration) targetSeconds = session.Duration;

        session.PlayOffset = targetSeconds;
        session.StartTime = _timing.RealTime;

        SyncSessionWithAllPlayers(session);
        UpdateAllEuis();
    }

    public List<SharedAdminAudioSession> GetActiveSharedSessions()
    {
        var result = new List<SharedAdminAudioSession>();
        var curTime = _timing.RealTime;

        foreach (var s in _audioSessions.Values)
        {
            float liveOffset = s.PlayOffset;
            if (s.IsPlaying)
            {
                liveOffset += (float)(curTime - s.StartTime).TotalSeconds;
                if (liveOffset > s.Duration) liveOffset = s.Duration;
            }
            else if (liveOffset >= s.Duration) liveOffset = 0f;

            result.Add(s.ToShared((float)Math.Round(liveOffset, 2)));
        }
        return result;
    }

    public List<string> GetAvailableMaps()
    {
        var result = new List<string>();
        var query = EntityQueryEnumerator<Robust.Shared.Map.Components.MapComponent>();
        while (query.MoveNext(out var uid, out _)) result.Add(GetNetEntity(uid).ToString());
        return result;
    }

    public List<string> GetTrackListForFolder(string folderPath)
    {
        var tracks = new List<string>();
        var cleanFolderPath = folderPath.EndsWith("/") ? folderPath : folderPath + "/";
        foreach (var file in _resourceManager.ContentFindFiles(new ResPath(cleanFolderPath)))
        {
            if (!file.ToString().EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) continue;
            if (file.ToString().Substring(cleanFolderPath.Length).Contains('/')) continue;
            tracks.Add(file.Filename.Substring(0, file.Filename.Length - 4));
        }
        return tracks;
    }

    public void CacheTracksFromFolder(string folderPath)
    {
        _availableTracks.Clear();
        if (string.IsNullOrWhiteSpace(folderPath) || !folderPath.StartsWith("/")) return;
        _currentFolderPath = folderPath;
        foreach (var track in GetTrackListForFolder(folderPath)) _availableTracks[track] = folderPath + track + ".ogg";
    }

    public Dictionary<string, string> GetTrackList() => _availableTracks;
    public string GetCurrentFolderPath() => _currentFolderPath;
    public void AddActiveEui(MusicPlayerEui eui) => _activeEuis.Add(eui);
    public void RemoveActiveEui(MusicPlayerEui eui) => _activeEuis.Remove(eui);
	
    public void UpdateAllEuis()
    {
        for (int i = _activeEuis.Count - 1; i >= 0; i--)
        {
            var eui = _activeEuis[i];
            if (eui.Player == null || eui.Player.Status != Robust.Shared.Enums.SessionStatus.InGame)
            {
                _activeEuis.RemoveAt(i);
                continue;
            }
            try { eui.StateDirty(); } catch { _activeEuis.RemoveAt(i); }
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev) { _activeEuis.Clear(); _audioSessions.Clear(); _playerLastSyncTime.Clear(); }
    public void RenameSession(string actorCkey, Guid sessionId, string newName) { if (_audioSessions.TryGetValue(sessionId, out var session)) { session.SessionName = newName; UpdateAllEuis(); } }

    public void ChangeSessionLoopMode(string actorCkey, Guid sessionId, MusicPlayerLoopMode newMode)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;

        _adminLogger.Add(LogType.Action, LogImpact.Low, 
            $"[MusicPlayer] Admin '{actorCkey}' changed loop mode on session '{session.SessionName}' to enum state: {newMode}.");

        session.LoopMode = newMode;
        UpdateAllEuis();
    }
	
    public void SelectPlaylistTrack(string actorCkey, Guid sessionId, int trackIndex)
    {
        if (!_audioSessions.TryGetValue(sessionId, out var session)) return;
        if (trackIndex < 0 || trackIndex >= session.PlaylistTracks.Count) return;

        var targetPath = session.PlaylistTracks[trackIndex];
        if (!session.TracksDurationCache.TryGetValue(targetPath, out float newDuration)) newDuration = 180f;
        if (newDuration < 1.5f) newDuration = 1.5f;

        session.SwitchToTrack(trackIndex, newDuration);
        
        var curTime = _timing.RealTime;
        session.StartTime = curTime;
        session.ExpireTime = curTime + TimeSpan.FromSeconds(newDuration);
        
        _adminLogger.Add(LogType.Action, LogImpact.Low, 
            $"[MusicPlayer] Admin '{actorCkey}' manually switched session '{session.SessionName}' playlist timeline to track index {trackIndex + 1} ('{session.TrackName}').");

        SyncSessionWithAllPlayers(session);
        UpdateAllEuis();
    }
}

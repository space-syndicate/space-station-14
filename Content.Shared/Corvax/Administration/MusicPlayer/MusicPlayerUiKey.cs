using System;
using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Network;

namespace Content.Shared.Corvax.Administration.MusicPlayer;

public enum MusicPlayerLoopMode : byte
{
    Off = 0,
    One = 1,
    All = 2
}

[Serializable, NetSerializable]
public sealed class SharedAdminAudioSession
{
    public Guid SessionId { get; }
    public string SessionName { get; set; }
    public string CreatorCkey { get; }
    public string TrackName { get; }
    public bool IsPlaying { get; set; }
    public int Volume { get; set; }
    
    public float Duration { get; } 
    public float PlayOffset { get; set; } 

    public List<string> PlaylistTracks { get; }
    public int CurrentTrackIndex { get; }
    
    public MusicPlayerLoopMode LoopMode { get; }
    
    public bool IsPlaylist => PlaylistTracks != null && PlaylistTracks.Count > 1;

    public List<string> Maps { get; }
    public List<string> Players { get; }

    public SharedAdminAudioSession(
        Guid sessionId, 
        string sessionName, 
        string creatorCkey, 
        string trackName, 
        bool isPlaying, 
        int volume, 
        float duration,
        float playOffset,
        List<string> playlistTracks,
        int currentTrackIndex,
        MusicPlayerLoopMode loopMode,
        List<string> maps, 
        List<string> players)
    {
        SessionId = sessionId;
        SessionName = sessionName;
        CreatorCkey = creatorCkey;
        TrackName = trackName;
        IsPlaying = isPlaying;
        Volume = volume;
        Duration = duration;
        PlayOffset = playOffset;
        PlaylistTracks = playlistTracks;
        CurrentTrackIndex = currentTrackIndex;
        LoopMode = loopMode;
        Maps = maps;
        Players = players;
    }
}

[Serializable, NetSerializable]
public sealed class MusicPlayerCreateSessionMessage : EuiMessageBase
{
    public List<string> TrackPaths { get; }
    public string TrackName { get; }
    public int StartVolume { get; }
    
    public Dictionary<string, float> TrackDurations { get; }
    
    public List<string>? SelectedMaps { get; }
    public List<string>? SelectedPlayers { get; }

    public MusicPlayerCreateSessionMessage(
        List<string> trackPaths, 
        string trackName, 
        int startVolume, 
        Dictionary<string, float> trackDurations,
        List<string>? selectedMaps, 
        List<string>? selectedPlayers)
    {
        TrackPaths = trackPaths;
        TrackName = trackName;
        StartVolume = startVolume;
        TrackDurations = trackDurations; 
        SelectedMaps = selectedMaps;
        SelectedPlayers = selectedPlayers;
    }
}

[Serializable, NetSerializable]
public sealed class MusicPlayerTogglePauseMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public MusicPlayerTogglePauseMessage(Guid sessionId) => SessionId = sessionId;
}

[Serializable, NetSerializable]
public sealed class MusicPlayerStopSessionMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public MusicPlayerStopSessionMessage(Guid sessionId) => SessionId = sessionId;
}

[Serializable, NetSerializable]
public sealed class MusicPlayerChangeSessionVolumeMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public int NewVolume { get; }
    public MusicPlayerChangeSessionVolumeMessage(Guid sessionId, int newVolume)
    {
        SessionId = sessionId;
        NewVolume = newVolume;
    }
}

[Serializable, NetSerializable]
public sealed class MusicPlayerRenameSessionMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public string NewName { get; }
    public MusicPlayerRenameSessionMessage(Guid sessionId, string newName)
    {
        SessionId = sessionId;
        NewName = newName;
    }
}

[Serializable, NetSerializable]
public sealed class MusicPlayerSeekSessionMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public float TargetSeconds { get; } 

    public MusicPlayerSeekSessionMessage(Guid sessionId, float targetSeconds)
    {
        SessionId = sessionId;
        TargetSeconds = targetSeconds;
    }
}

[Serializable, NetSerializable]
public sealed class MusicPlayerScanFolderRequestMessage : EuiMessageBase
{
    public string FolderPath { get; }
    public MusicPlayerScanFolderRequestMessage(string folderPath) => FolderPath = folderPath;
}

[Serializable, NetSerializable]
public sealed class MusicPlayerRefreshTargetsMessage : EuiMessageBase
{
}

[Serializable, NetSerializable]
public sealed class MusicPlayerEuiState : EuiStateBase
{
    public List<string> AvailableMaps { get; } 
    public List<string> AvailablePlayers { get; }
    public List<string> ScannedTracks { get; }
    public string CurrentFolderPath { get; }
    public List<SharedAdminAudioSession> ActiveSessions { get; }

    public MusicPlayerEuiState(
        List<string> availableMaps, 
        List<string> availablePlayers, 
        List<string> scannedTracks,
        string currentFolderPath,
        List<SharedAdminAudioSession> activeSessions)
    {
        AvailableMaps = availableMaps;
        AvailablePlayers = availablePlayers;
        ScannedTracks = scannedTracks;
        CurrentFolderPath = currentFolderPath;
        ActiveSessions = activeSessions;
    }
}

[Serializable, NetSerializable]
public sealed class AdminMusicClientPlayMessage : EntityEventArgs
{
    public Guid SessionId { get; }
    public string TrackPath { get; }
    public float TargetSeconds { get; }
    public float Volume { get; }

    public AdminMusicClientPlayMessage(
        Guid sessionId, string trackPath, float targetSeconds, float volume)
    {
        SessionId = sessionId;
        TrackPath = trackPath;
        TargetSeconds = targetSeconds;
        Volume = volume;
    }
}

[Serializable, NetSerializable]
public sealed class AdminMusicClientStopMessage : EntityEventArgs
{
    public Guid SessionId { get; }
    public AdminMusicClientStopMessage(Guid sessionId) => SessionId = sessionId;
}

[Serializable, NetSerializable]
public sealed class MusicPlayerNextTrackMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public MusicPlayerNextTrackMessage(Guid sessionId) => SessionId = sessionId;
}

[Serializable, NetSerializable]
public sealed class MusicPlayerPrevTrackMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public MusicPlayerPrevTrackMessage(Guid sessionId) => SessionId = sessionId;
}

[Serializable, NetSerializable]
public sealed class MusicPlayerRemoveMapMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public string MapId { get; }
    public MusicPlayerRemoveMapMessage(Guid sessionId, string mapId)
    {
        SessionId = sessionId;
        MapId = mapId;
    }
}

[Serializable, NetSerializable]
public sealed class MusicPlayerRemovePlayerMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public string PlayerCkey { get; }
    public MusicPlayerRemovePlayerMessage(Guid sessionId, string playerCkey)
    {
        SessionId = sessionId;
        PlayerCkey = playerCkey;
    }
}

[Serializable, NetSerializable]
public sealed class AdminMusicClientTrackFinishedMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public AdminMusicClientTrackFinishedMessage(Guid sessionId) => SessionId = sessionId;
}

[Serializable, NetSerializable]
public sealed class MusicPlayerChangeLoopModeMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public MusicPlayerLoopMode NewMode { get; }

    public MusicPlayerChangeLoopModeMessage(Guid sessionId, MusicPlayerLoopMode newMode)
    {
        SessionId = sessionId;
        NewMode = newMode;
    }
}

[Serializable, NetSerializable]
public sealed class MusicPlayerSelectPlaylistTrackMessage : EuiMessageBase
{
    public Guid SessionId { get; }
    public int TrackIndex { get; }

    public MusicPlayerSelectPlaylistTrackMessage(Guid sessionId, int trackIndex)
    {
        SessionId = sessionId;
        TrackIndex = trackIndex;
    }
}
using System;
using Content.Server.EUI;
using Content.Shared.Corvax.Administration.MusicPlayer;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Corvax.Administration.MusicPlayer;

public sealed partial class MusicPlayerEui : BaseEui
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private string _myFolderPath = "/Audio/Lobby/";

    public override void Opened()
    {
        var system = _entityManager.System<MusicPlayerSystem>();
        system.AddActiveEui(this);
		
        system.CacheTracksFromFolder(_myFolderPath);
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var system = _entityManager.System<MusicPlayerSystem>();
        
        var trackNames = new System.Collections.Generic.List<string>(system.GetTrackListForFolder(_myFolderPath));

        var playerCkeys = new System.Collections.Generic.List<string>();
        foreach (var playerSession in _playerManager.Sessions)
        {
            if (!string.IsNullOrEmpty(playerSession.Name))
                playerCkeys.Add(playerSession.Name);
        }

        var availableMaps = system.GetAvailableMaps();
        var activeSessions = system.GetActiveSharedSessions();

        return new MusicPlayerEuiState(
            availableMaps,
            playerCkeys,
            trackNames,
            _myFolderPath, 
            activeSessions
        );
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        var system = _entityManager.System<MusicPlayerSystem>();
        var actorCkey = Player?.Name ?? "UnknownAdmin";

        switch (msg)
        {
            case MusicPlayerRefreshTargetsMessage:
                StateDirty();
                break;

            case MusicPlayerScanFolderRequestMessage scanMsg:
                _myFolderPath = scanMsg.FolderPath;
                if (!_myFolderPath.StartsWith("/")) _myFolderPath = "/" + _myFolderPath;
                if (!_myFolderPath.EndsWith("/")) _myFolderPath += "/";

                system.CacheTracksFromFolder(_myFolderPath);
                StateDirty();
                break;

            case MusicPlayerCreateSessionMessage createMsg:
                system.CreateAudioSession(
                    actorCkey,
                    createMsg.TrackPaths,
                    createMsg.TrackName,
                    createMsg.StartVolume,
                    createMsg.TrackDurations,
                    createMsg.SelectedMaps,
                    createMsg.SelectedPlayers
                );
                break;
            case MusicPlayerTogglePauseMessage pauseMsg:
                system.TogglePauseSession(actorCkey, pauseMsg.SessionId);
                break;
            case MusicPlayerStopSessionMessage stopMsg:
                system.StopSession(actorCkey, stopMsg.SessionId);
                break;
            case MusicPlayerChangeSessionVolumeMessage volumeMsg:
                system.ChangeSessionVolume(actorCkey, volumeMsg.SessionId, volumeMsg.NewVolume);
                break;
            case MusicPlayerRenameSessionMessage renameMsg:
                system.RenameSession(actorCkey, renameMsg.SessionId, renameMsg.NewName);
                break;
            case MusicPlayerNextTrackMessage nextMsg:
                system.NextTrack(nextMsg.SessionId);
                break;
            case MusicPlayerPrevTrackMessage prevMsg:
                system.PrevTrack(prevMsg.SessionId);
                break;
            case MusicPlayerRemoveMapMessage removeMapMsg:
                system.RemoveMapFromSession(actorCkey, removeMapMsg.SessionId, removeMapMsg.MapId);
                break;
            case MusicPlayerRemovePlayerMessage removePlayerMsg:
                system.RemovePlayerFromSession(actorCkey, removePlayerMsg.SessionId, removePlayerMsg.PlayerCkey);
                break;
            case MusicPlayerChangeLoopModeMessage loopMsg:
                system.ChangeSessionLoopMode(actorCkey, loopMsg.SessionId, loopMsg.NewMode);
                break;
            case MusicPlayerSelectPlaylistTrackMessage selectTrackMsg:
                system.SelectPlaylistTrack(actorCkey, selectTrackMsg.SessionId, selectTrackMsg.TrackIndex);
                break;
        }
    }
}



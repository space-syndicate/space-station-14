using System;
using System.Collections.Generic;
using Content.Shared.Corvax.Administration.MusicPlayer;
using Content.Shared.Eui;
using Content.Client.Eui;
using Robust.Client.Graphics;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.Corvax.Administration.MusicPlayer;

[UsedImplicitly]
public sealed partial class MusicPlayerEui : BaseEui
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private Robust.Client.Player.IPlayerManager _playerManager = default!;
	
    private AdminMusicAudioSystem _adminAudioSystem = default!;
    private readonly MusicPlayerWindow _window;
    
    private readonly Dictionary<Guid, AudioSessionDetailsWindow> _openDetailsWindows = 
        new Dictionary<Guid, AudioSessionDetailsWindow>();
	
    private readonly List<string> _mapIdCache = new List<string>(); 
    private readonly List<string> _playerCkeyCache = new List<string>();
    
    private int _lastMapsCount = -1;
    private int _lastPlayersCount = -1;
    private int _localCategory = 0;
    private MusicPlayerEuiState? _lastServerState;

    public MusicPlayerEui()
    {
        _window = new MusicPlayerWindow();
        
        _window.CategorySelector.OnItemSelected += args =>
        {
            _window.CategorySelector.SelectId(args.Id);
            _localCategory = args.Id;
            _window.SearchInput.Text = string.Empty;
            RebuildTiles(_lastServerState); 
        };

        _window.OnFolderChanged += folderPath => SendMessage(new MusicPlayerScanFolderRequestMessage(folderPath ?? ""));
        _window.OnRefreshMusicPressed += () => SendMessage(new MusicPlayerScanFolderRequestMessage(_window.FolderInput.Text ?? ""));
        _window.OnRefreshTargetsPressed += () => SendMessage(new MusicPlayerRefreshTargetsMessage());

        _window.OnCreateSessionPressed += (paths, name, vol, durations, maps, players) =>
        {
            SendMessage(new MusicPlayerCreateSessionMessage(paths, name, vol, durations, maps, players));
            RebuildTiles(_lastServerState);
        };

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        base.Opened();
        _adminAudioSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AdminMusicAudioSystem>();
        _window.Open();

        if (_lastServerState != null)
        {
            HandleState(_lastServerState);
        }
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();

        foreach (var detailsWindow in _openDetailsWindows.Values)
        {
            detailsWindow.Close();
        }
        _openDetailsWindows.Clear();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is AdminMusicClientTrackFinishedMessage finishedMsg)
        {
            foreach (var child in _window.ActiveSessionsList.Children)
            {
                if (child is ActiveSessionRow row && row.TargetSessionId == finishedMsg.SessionId)
                {
                    row.ForceTrackFinishedPlaybackStyle();
                    break;
                }
            }
            
            if (_openDetailsWindows.TryGetValue(finishedMsg.SessionId, out var detWin) && _lastServerState != null)
            {
                foreach (var s in _lastServerState.ActiveSessions)
                {
                    if (s.SessionId == finishedMsg.SessionId)
                    {
                        detWin.PopulateData(s);
                        break;
                    }
                }
            }
        }
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not MusicPlayerEuiState playerState) return;

        _lastServerState = playerState;

        _mapIdCache.Clear();
        _mapIdCache.AddRange(playerState.AvailableMaps);

        _playerCkeyCache.Clear();
        _playerCkeyCache.AddRange(playerState.AvailablePlayers);

        RebuildTiles(playerState);

        _window.ActiveSessionsList.RemoveAllChildren();

        foreach (var session in playerState.ActiveSessions)
        {
            var capturedId = session.SessionId;
            var sessionRow = new ActiveSessionRow(session);
            
            sessionRow.PauseBtn.OnPressed += _ => SendMessage(new MusicPlayerTogglePauseMessage(capturedId));
            sessionRow.StopBtn.OnPressed += _ => SendMessage(new MusicPlayerStopSessionMessage(capturedId));
            sessionRow.PrevBtn.OnPressed += _ => SendMessage(new MusicPlayerPrevTrackMessage(capturedId));
            sessionRow.NextBtn.OnPressed += _ => SendMessage(new MusicPlayerNextTrackMessage(capturedId));
            sessionRow.OnRenameRequested += (id, newName) => SendMessage(new MusicPlayerRenameSessionMessage(id, newName));

            sessionRow.VolInput.OnTextEntered += args => {
                if (int.TryParse(args.Text, out var parsedVol))
                {
                    SendMessage(new MusicPlayerChangeSessionVolumeMessage(capturedId, parsedVol));
                }
                sessionRow.ApplyInPlaceVolume();
            };

            sessionRow.LoopBtn.OnPressed += _ =>
            {
                var confirmedNextMode = sessionRow.ToggleLoopModeLocally();
                SendMessage(new MusicPlayerChangeLoopModeMessage(capturedId, confirmedNextMode));
            };

            sessionRow.InfoTrackBtn.OnPressed += _ => { OpenDetailsWindow(capturedId, sessionRow.CurrentSessionData); };
            _window.ActiveSessionsList.AddChild(sessionRow);

            if (_openDetailsWindows.TryGetValue(session.SessionId, out var detWin))
            {
                detWin.PopulateData(session);
            }
        }

        _window.TrackListContainer.RemoveAllChildren();
        foreach (var trackName in playerState.ScannedTracks)
        {
            var tileWrapper = new PanelContainer { HorizontalExpand = true, Margin = new Thickness(0, 1) };
            var trackBtn = new Button { HorizontalExpand = true, MinHeight = 24, ToggleMode = true };
            trackBtn.AddStyleClass("ButtonSquare");

            var trackLabel = new Label {
                Text = trackName, HorizontalExpand = true, VerticalExpand = true,
                Align = Label.AlignMode.Left, ClipText = true, Margin = new Thickness(8, 0, 0, 0)
            };
            trackBtn.AddChild(trackLabel);

            if (_window.GetSelectedTracksCache().Contains(trackName)) trackBtn.Pressed = true;

            var capturedTrack = trackName;
            trackBtn.OnPressed += _ =>
            {
                if (_window.GetSelectedTracksCache().Contains(capturedTrack))
                    _window.GetSelectedTracksCache().Remove(capturedTrack);
                else
                    _window.GetSelectedTracksCache().Add(capturedTrack);

                HandleState(playerState);
            };

            tileWrapper.AddChild(trackBtn);
            _window.TrackListContainer.AddChild(tileWrapper);
        }
    }

    private void OpenDetailsWindow(Guid id, SharedAdminAudioSession session)
    {
        if (_openDetailsWindows.TryGetValue(id, out var existingWindow))
        {
            existingWindow.MoveToFront();
            return;
        }

        var detailsWindow = new AudioSessionDetailsWindow();
        detailsWindow.OnRemoveMapRequested += (sId, mId) => SendMessage(new MusicPlayerRemoveMapMessage(sId, mId));
        detailsWindow.OnRemovePlayerRequested += (sId, pCkey) => SendMessage(new MusicPlayerRemovePlayerMessage(sId, pCkey));
        detailsWindow.OnClose += () => _openDetailsWindows.Remove(id);
        detailsWindow.OnTrackSelectRequested += (sId, tIdx) => SendMessage(new MusicPlayerSelectPlaylistTrackMessage(sId, tIdx));

        detailsWindow.PopulateData(session);
        detailsWindow.OpenCentered();
        detailsWindow.OnMiniPausePressed += sId => SendMessage(new MusicPlayerTogglePauseMessage(sId));

        _openDetailsWindows[id] = detailsWindow;
    }

    private void RebuildTiles(MusicPlayerEuiState? playerState)
    {
        _window.TargetsGrid.RemoveAllChildren();

        if (_localCategory == 0)
        {
            foreach (var mapId in _mapIdCache)
            {
                string mapFriendlyName = $"Map: {mapId}";

                if (int.TryParse(mapId, out var idInt))
                {
                    var netEntity = new Robust.Shared.GameObjects.NetEntity(idInt);
                    
                    if (_entManager.TryGetEntity(netEntity, out var mapUid))
                    {
                        if (_entManager.TryGetComponent<Robust.Shared.Map.Components.MapComponent>(mapUid, out var mapComp))
                        {
                            if (_entManager.TryGetComponent<Robust.Shared.GameObjects.MetaDataComponent>(mapUid, out var meta))
                            {
                                var numericMapId = (int) mapComp.MapId;
                                mapFriendlyName = $"{meta.EntityName}: {numericMapId}";
                            }
                        }
                    }
                }

                var tileButton = new Button { MinWidth = 125, MinHeight = 35, ToggleMode = true, HorizontalExpand = true };
                var tileLabel = new Label {
                    Text = mapFriendlyName, HorizontalExpand = true, VerticalExpand = true,
                    Align = Label.AlignMode.Left, ClipText = true, Margin = new Thickness(8, 0, 0, 0)
                };
                tileButton.AddChild(tileLabel);

                if (_window.SelectedMapsCache.Contains(mapId)) tileButton.Pressed = true;

                var capturedMapId = mapId;
                tileButton.OnPressed += _ =>
                {
                    if (_window.SelectedMapsCache.Contains(capturedMapId))
                        _window.SelectedMapsCache.Remove(capturedMapId);
                    else
                        _window.SelectedMapsCache.Add(capturedMapId);
                };

                _window.TargetsGrid.AddChild(tileButton);
            }
        }
        else if (_localCategory == 1)
        {
            foreach (var playerCkey in _playerCkeyCache)
            {
                var tileButton = new Button { MinWidth = 125, MinHeight = 35, ToggleMode = true, HorizontalExpand = true };
                var tileLabel = new Label {
                    Text = playerCkey, HorizontalExpand = true, VerticalExpand = true,
                    Align = Label.AlignMode.Left, ClipText = true, Margin = new Thickness(8, 0, 0, 0)
                };
                tileButton.AddChild(tileLabel);

                if (_window.SelectedPlayersCache.Contains(playerCkey)) tileButton.Pressed = true;

                var capturedCkey = playerCkey;
                tileButton.OnPressed += _ =>
                {
                    if (_window.SelectedPlayersCache.Contains(capturedCkey))
                        _window.SelectedPlayersCache.Remove(capturedCkey);
                    else
                        _window.SelectedPlayersCache.Add(capturedCkey);
                };

                _window.TargetsGrid.AddChild(tileButton);
            }
        }
        _window.SearchInput.Text = _window.SearchInput.Text;
    }
}

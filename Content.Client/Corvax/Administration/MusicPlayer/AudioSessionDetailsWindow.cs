using System;
using System.Numerics;
using Content.Shared.Corvax.Administration.MusicPlayer;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Content.Client.Corvax.Administration.MusicPlayer;

public sealed class AudioSessionDetailsWindow : DefaultWindow
{
    private readonly BoxContainer _listenersList;
    private readonly BoxContainer _playlistList;
    
    public readonly Button MiniPauseBtn;
    private readonly Label _miniTimeLabel;

    private Guid _sessionId;
    private double _clientPlayOffset;
    private float _trackDuration;
    private bool _isPlaying;

    public Action<Guid, string>? OnRemoveMapRequested;
    public Action<Guid, string>? OnRemovePlayerRequested;
    public Action<Guid, int>? OnTrackSelectRequested;
    
    public Action<Guid>? OnMiniPausePressed;

    public AudioSessionDetailsWindow()
    {
        SetSize = new Vector2(580, 360);
        Title = Loc.GetString("admin-music-player-details-title");

        var mainLayout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(10, 35, 10, 10) 
        };

        var cardsLayout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12
        };

        var cardStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#141416"),
            BorderThickness = new Thickness(1),
            BorderColor = Color.FromHex("#25252a"),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8
        };

        var leftCard = new PanelContainer { HorizontalExpand = true, VerticalExpand = true, PanelOverride = cardStyle };
        var leftLayout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
        
        leftLayout.AddChild(new Label { Text = Loc.GetString("admin-music-player-header-listeners"), Margin = new Thickness(0, 0, 0, 6), FontColorOverride = Color.FromHex("#A0DBF5") });
        var leftScroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _listenersList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };

        leftScroll.AddChild(_listenersList);
        leftLayout.AddChild(leftScroll);
        
        leftCard.AddChild(leftLayout);
        cardsLayout.AddChild(leftCard);

        var rightCard = new PanelContainer { HorizontalExpand = true, VerticalExpand = true, PanelOverride = cardStyle };
        var rightLayout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
        
        rightLayout.AddChild(new Label { Text = Loc.GetString("admin-music-player-header-playlist"), Margin = new Thickness(0, 0, 0, 6), FontColorOverride = Color.FromHex("#A0DBF5") });
        var rightScroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _playlistList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        rightScroll.AddChild(_playlistList);
        rightLayout.AddChild(rightScroll);
        
        rightCard.AddChild(rightLayout);
        cardsLayout.AddChild(rightCard);

        mainLayout.AddChild(cardsLayout);

        var miniFooter = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 10, 0, 0),
            VerticalAlignment = VAlignment.Center
        };

        MiniPauseBtn = new Button { Text = " ▶ ", MinWidth = 45, MinHeight = 26 };
        MiniPauseBtn.OnPressed += _ => OnMiniPausePressed?.Invoke(_sessionId);
        miniFooter.AddChild(MiniPauseBtn);

        var spacer = new Control { HorizontalExpand = true };
        miniFooter.AddChild(spacer);

        _miniTimeLabel = new Label { Text = "00:00 / 00:00", MinWidth = 90, VerticalAlignment = VAlignment.Center, HorizontalAlignment = HAlignment.Right };
        miniFooter.AddChild(_miniTimeLabel);

        mainLayout.AddChild(miniFooter);
        AddChild(mainLayout);
    }

    public void PopulateData(SharedAdminAudioSession session)
    {
        _sessionId = session.SessionId;
        _isPlaying = session.IsPlaying;
        _trackDuration = session.Duration;

        MiniPauseBtn.Text = _isPlaying ? " || " : " ▶ ";

        if (!_isPlaying || Math.Abs(_clientPlayOffset - session.PlayOffset) > 1.5f)
        {
            _clientPlayOffset = session.PlayOffset;
            UpdateMiniTimeString();
        }

        _listenersList.RemoveAllChildren();
        _playlistList.RemoveAllChildren();

        var entManager = Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.GameObjects.IEntityManager>();

        foreach (var map in session.Maps)
        {
            string mapFriendlyName = Loc.GetString("admin-music-player-map-prefix", ("map", map));

            if (int.TryParse(map, out var idInt))
            {
                var netEntity = new Robust.Shared.GameObjects.NetEntity(idInt);
                
                if (entManager.TryGetEntity(netEntity, out var mapUid))
                {
                    if (entManager.TryGetComponent<Robust.Shared.Map.Components.MapComponent>(mapUid, out var mapComp))
                    {
                        if (entManager.TryGetComponent<Robust.Shared.GameObjects.MetaDataComponent>(mapUid, out var meta))
                        {
                            var numericMapId = (int) mapComp.MapId;
                            mapFriendlyName = $"{meta.EntityName}: {numericMapId}";
                        }
                    }
                }
            }

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, Margin = new Thickness(0, 1) };
            row.AddChild(new Label { Text = mapFriendlyName, HorizontalExpand = true });
            
            var removeBtn = new Button { Text = "X", StyleClasses = { "ButtonColorRed" }, MinWidth = 24 };
            removeBtn.OnPressed += _ => OnRemoveMapRequested?.Invoke(_sessionId, map);
            row.AddChild(removeBtn);
            _listenersList.AddChild(row);
        }

        foreach (var player in session.Players)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, Margin = new Thickness(0, 1) };
            row.AddChild(new Label { Text = player, HorizontalExpand = true, ClipText = true });
            
            var removeBtn = new Button { Text = "X", StyleClasses = { "ButtonColorRed" }, MinWidth = 24 };
            removeBtn.OnPressed += _ => OnRemovePlayerRequested?.Invoke(_sessionId, player);
            row.AddChild(removeBtn);
            _listenersList.AddChild(row);
        }

        if (session.PlaylistTracks != null)
        {
            for (int i = 0; i < session.PlaylistTracks.Count; i++)
            {
                var trackPath = session.PlaylistTracks[i];
                var lastSlash = trackPath.LastIndexOf('/');
                var trackName = lastSlash != -1 ? trackPath.Substring(lastSlash + 1) : trackPath;
                
                if (trackName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    trackName = trackName.Substring(0, trackName.Length - 4);

                var trackPlate = new Button
                {
                    Text = $"{i + 1}. {trackName}",
                    HorizontalExpand = true,
                    MinHeight = 26,
                    StyleClasses = { "ButtonColorDark" },
                    TextAlign = Label.AlignMode.Left,
                    Margin = new Thickness(0, 1),
                    ToggleMode = true 
                };
                trackPlate.AddStyleClass("ButtonSquare");

                if (i == session.CurrentTrackIndex)
                {
                    trackPlate.Pressed = true; 
                }
                else
                {
                    trackPlate.Pressed = false; 
                }

                var trackIndex = i;
                trackPlate.OnPressed += _ => OnTrackSelectRequested?.Invoke(_sessionId, trackIndex);

                _playlistList.AddChild(trackPlate);
            }
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_isPlaying) return;

        _clientPlayOffset += args.DeltaSeconds;
        if (_clientPlayOffset > _trackDuration)
            _clientPlayOffset = _trackDuration;

        UpdateMiniTimeString();
    }

    private void UpdateMiniTimeString()
    {
        var current = TimeSpan.FromSeconds(_clientPlayOffset);
        var total = TimeSpan.FromSeconds(_trackDuration);
        _miniTimeLabel.Text = $"{current:mm\\:ss} / {total:mm\\:ss}";
    }
}

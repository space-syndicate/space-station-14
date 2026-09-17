using System;
using System.Numerics;
using Content.Shared.Corvax.Administration.MusicPlayer;
using Robust.Client.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Corvax.Administration.MusicPlayer;

public sealed class ActiveSessionRow : BoxContainer
{
    private SharedAdminAudioSession _session;
    public Guid TargetSessionId => _session.SessionId;
    public SharedAdminAudioSession CurrentSessionData => _session;

    private readonly Label _nameLabel;
    private readonly LineEdit _nameInput;
    private readonly Label _timeLabel;
    
    public readonly Button RenameBtn;
    public readonly Button PrevBtn;
    public readonly Button PauseBtn;
    public readonly Button NextBtn;
    public readonly Button InfoTrackBtn;
    public readonly Button StopBtn;

    public readonly Button VolBtn;         
    private readonly Label _volumeLabel;  
    public readonly LineEdit VolInput;    
    public readonly Button LoopBtn;      

    private double _clientPlayOffset; 
    private bool _isPlaying;
    private bool _isEditingName;
    private bool _isEditingVolume; 

    public string CurrentName => _session.SessionName;
    public int CurrentVolume => _session.Volume;
    public bool CurrentIsPlaying => _session.IsPlaying;

    public Action<Guid, string>? OnRenameRequested;

    public ActiveSessionRow(SharedAdminAudioSession session)
    {
        _session = session;
        _isPlaying = session.IsPlaying;
        _clientPlayOffset = session.PlayOffset; 

        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(0, 4, 0, 12); 
        HorizontalExpand = true;

        var mainCardFrame = new PanelContainer
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 2, 0, 2)
        };

        var masterFrameStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#141416"), 
            BorderThickness = new Thickness(1),        
            BorderColor = Color.FromHex("#25252a"),     
            ContentMarginLeftOverride = 0,            
            ContentMarginRightOverride = 0,
            ContentMarginTopOverride = 0,
            ContentMarginBottomOverride = 6 
        };
        mainCardFrame.PanelOverride = masterFrameStyle;

        var cardContentLayout = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        var cargoHeaderBg = new Content.Client.UserInterface.Controls.StripeBack
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var titleRow = new BoxContainer 
        { 
            Orientation = LayoutOrientation.Horizontal, 
            HorizontalExpand = true,
            Margin = new Thickness(8, 4, 8, 4) 
        };

        _nameLabel = new Label { 
            Text = string.IsNullOrEmpty(session.SessionName) ? " " : session.SessionName, 
            HorizontalExpand = true, 
            FontColorOverride = Color.FromHex("#A0DBF5"), 
            ClipText = true
        };
        titleRow.AddChild(_nameLabel);

        _nameInput = new LineEdit { Text = session.SessionName, HorizontalExpand = true, Visible = false };
        _nameInput.OnTextEntered += _ => ApplyInPlaceRename();
        titleRow.AddChild(_nameInput);

        RenameBtn = new Button { Text = Loc.GetString("admin-music-player-session-rename"), MinWidth = 60, Margin = new Thickness(6, 0, 0, 0) };
        RenameBtn.OnPressed += _ => ToggleNameEditMode();
        titleRow.AddChild(RenameBtn);

        cargoHeaderBg.AddChild(titleRow);
        cardContentLayout.AddChild(cargoHeaderBg);

        var middleRow = new BoxContainer { 
            Orientation = LayoutOrientation.Horizontal, 
            HorizontalExpand = true, 
            Margin = new Thickness(8, 2, 8, 6)
        };

        InfoTrackBtn = new Button { HorizontalExpand = true, MinWidth = 120, ClipText = true, StyleClasses = { "ButtonColorDark" } };
        middleRow.AddChild(InfoTrackBtn);

        _timeLabel = new Label { Text = "00:00 / 00:00", MinWidth = 90, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VAlignment.Center, HorizontalAlignment = HAlignment.Right };
        middleRow.AddChild(_timeLabel);
        cardContentLayout.AddChild(middleRow); 

        var controlsRow = new BoxContainer { 
            Orientation = LayoutOrientation.Horizontal, 
            HorizontalExpand = true,
            Margin = new Thickness(8, 0, 8, 2) 
        };

        var volumeGroup = new BoxContainer { Orientation = LayoutOrientation.Horizontal, MinWidth = 85 };
        VolBtn = new Button { Text = "vol.", MinWidth = 40, VerticalAlignment = VAlignment.Center };
        VolBtn.OnPressed += _ => ToggleVolumeEditMode();
        volumeGroup.AddChild(VolBtn);

        _volumeLabel = new Label { 
            MinWidth = 35, 
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            FontColorOverride = Color.FromHex("#CCCCCC")
        };
        volumeGroup.AddChild(_volumeLabel);

        VolInput = new LineEdit { MinWidth = 55, Visible = false, VerticalAlignment = VAlignment.Center };
        volumeGroup.AddChild(VolInput);
        controlsRow.AddChild(volumeGroup);

        LoopBtn = new Button { 
            MinWidth = 60, 
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { "ButtonColorDark" }
        };
        controlsRow.AddChild(LoopBtn);

        var spacer = new Control { HorizontalExpand = true };
        controlsRow.AddChild(spacer);

        var rightControlsGrid = new GridContainer { 
            Columns = 4, 
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Right
        };
        
        PrevBtn = new Button { Text = " « ", MinWidth = 34, Margin = new Thickness(0, 0, 4, 0) };
        rightControlsGrid.AddChild(PrevBtn);

        PauseBtn = new Button { Text = _isPlaying ? " || " : " ▶ ", MinWidth = 36, Margin = new Thickness(0, 0, 4, 0) };
        rightControlsGrid.AddChild(PauseBtn);

        NextBtn = new Button { Text = " » ", MinWidth = 34, Margin = new Thickness(0, 0, 6, 0) };
        rightControlsGrid.AddChild(NextBtn);

        StopBtn = new Button 
        { 
            Text = "  X  ", 
            MinWidth = 36, 
            StyleClasses = { "ButtonColorRed" },
            ToggleMode = true,
            Pressed = true 
        };
        rightControlsGrid.AddChild(StopBtn);

        controlsRow.AddChild(rightControlsGrid);
        cardContentLayout.AddChild(controlsRow);

        mainCardFrame.AddChild(cardContentLayout);
        AddChild(mainCardFrame);
        
        UpdateInfoButtonText();
        UpdateVolumeVisuals();
        UpdateLoopButtonText();
        ApplyPlaylistButtonVisibility();
        UpdateTimeString();
    }

    private void UpdateInfoButtonText()
    {
        string targetTypeStr;
        
        // Проверяем сетевые коллекции напрямую в обход старого текстового свойства
        if (_session.Players != null && _session.Players.Count > 0)
            targetTypeStr = Loc.GetString("admin-music-player-target-type-players");
        else if (_session.Maps != null && _session.Maps.Count > 0)
            targetTypeStr = Loc.GetString("admin-music-player-target-type-maps");
        else
            targetTypeStr = Loc.GetString("admin-music-player-target-type-none");

        InfoTrackBtn.Text = $"{targetTypeStr} | {_session.TrackName}";
    }

    private void UpdateVolumeVisuals() => _volumeLabel.Text = $"({_session.Volume})";

    private void UpdateLoopButtonText()
    {
        LoopBtn.Text = _session.LoopMode switch
        {
            MusicPlayerLoopMode.Off => "L: off",
            MusicPlayerLoopMode.One => "L: 1",
            MusicPlayerLoopMode.All => "L: all",
            _ => "L: off"
        };
    }

    private void ApplyPlaylistButtonVisibility()
    {
        PrevBtn.Visible = _session.IsPlaylist;
        NextBtn.Visible = _session.IsPlaylist;
    }

    public void UpdateSessionData(SharedAdminAudioSession updatedSession)
    {
        _session = updatedSession;
        _isPlaying = updatedSession.IsPlaying;

        if (!_isEditingName) _nameLabel.Text = string.IsNullOrEmpty(updatedSession.SessionName) ? " " : updatedSession.SessionName;
        if (!_isEditingVolume) _volumeLabel.Text = $"({updatedSession.Volume})";

        UpdateLoopButtonText(); 
        PauseBtn.Text = _isPlaying ? " || " : " ▶ "; 
        UpdateInfoButtonText();
        ApplyPlaylistButtonVisibility();

        if (!_isPlaying)
        {
            _clientPlayOffset = updatedSession.PlayOffset;
            UpdateTimeString();
            return;
        }

        if (Math.Abs(_clientPlayOffset - updatedSession.PlayOffset) > 1.5f)
        {
            _clientPlayOffset = updatedSession.PlayOffset;
            UpdateTimeString();
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_isPlaying) return;

        _clientPlayOffset += args.DeltaSeconds;
        if (_clientPlayOffset > _session.Duration) _clientPlayOffset = _session.Duration;
        UpdateTimeString();
    }

    private void UpdateTimeString()
    {
        var current = TimeSpan.FromSeconds(_clientPlayOffset);
        var total = TimeSpan.FromSeconds(_session.Duration);
        _timeLabel.Text = $"{current:mm\\:ss} / {total:mm\\:ss}";
    }

    public void ForceTrackFinishedPlaybackStyle()
    {
        _isPlaying = false;
        _clientPlayOffset = 0f;
        PauseBtn.Text = " ▶ ";
        UpdateTimeString();
    }

    private void ToggleNameEditMode()
    {
        if (!_isEditingName)
        {
            _isEditingName = true;
            _nameLabel.Visible = false;
            _nameInput.Visible = true;
            _nameInput.Text = _session.SessionName;
            _nameInput.GrabKeyboardFocus();
            RenameBtn.Text = Loc.GetString("admin-music-player-session-ok");
        }
        else { ApplyInPlaceRename(); }
    }

    private void ApplyInPlaceRename()
    {
        if (_isEditingName)
        {
            _isEditingName = false;
            _nameLabel.Visible = true;
            _nameInput.Visible = false;
            
            RenameBtn.Text = Loc.GetString("admin-music-player-session-rename");
            
            var targetText = _nameInput.Text ?? string.Empty;
            if (targetText != _session.SessionName) OnRenameRequested?.Invoke(_session.SessionId, targetText);
        }
    }

    private void ToggleVolumeEditMode()
    {
        if (!_isEditingVolume)
        {
            _isEditingVolume = true;
            VolBtn.Visible = false;
            _volumeLabel.Visible = false;
            VolInput.Visible = true;
            VolInput.Text = _session.Volume.ToString();
            VolInput.GrabKeyboardFocus();
        }
    }

    public void ApplyInPlaceVolume()
    {
        if (_isEditingVolume)
        {
            _isEditingVolume = false;
            VolBtn.Visible = true;
            _volumeLabel.Visible = true; 
            VolInput.Visible = false;
        }
    }
	
    public MusicPlayerLoopMode ToggleLoopModeLocally()
    {
        var nextMode = _session.LoopMode switch
        {
            MusicPlayerLoopMode.Off => MusicPlayerLoopMode.One,
            MusicPlayerLoopMode.One => MusicPlayerLoopMode.All,
            MusicPlayerLoopMode.All => MusicPlayerLoopMode.Off
        };

        _session = new SharedAdminAudioSession(
            _session.SessionId, _session.SessionName, _session.CreatorCkey, _session.TrackName,
            _session.IsPlaying, _session.Volume, _session.Duration, _session.PlayOffset,
            _session.PlaylistTracks, _session.CurrentTrackIndex, nextMode, 
            _session.Maps, _session.Players
        );

        UpdateLoopButtonText(); 
        return nextMode;
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.IoC;
using Robust.Shared.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.Administration.MusicPlayer;

public sealed class MusicPlayerWindow : DefaultWindow
{
    public readonly OptionButton CategorySelector;
    public readonly Button RefreshTargetsButton;
    public readonly LineEdit SearchInput;
    public readonly GridContainer TargetsGrid;
    public readonly ScrollContainer TargetsScroll;

    public readonly HistoryLineEdit FolderInput;
    public readonly Button RefreshMusicButton;
    public readonly BoxContainer TrackListContainer;
	
    private readonly HashSet<string> _selectedTracksCache = new HashSet<string>();
    public HashSet<string> GetSelectedTracksCache() => _selectedTracksCache;

    public readonly LineEdit VolumeInput;
    public readonly Button CreateSessionButton;

    public readonly BoxContainer RightPanelContainer;
    public readonly BoxContainer ActiveSessionsList;

    public readonly List<string> SelectedMapsCache = new();
    public readonly List<string> SelectedPlayersCache = new();

    private readonly List<string> _autocompleteSuggestions = new();
    private int _autocompleteIndex = -1;
    private string _lastPrefix = string.Empty;

    public Action<int>? OnCategoryChanged;
    public Action? OnRefreshTargetsPressed;
    public Action<string>? OnFolderChanged;
    public Action? OnRefreshMusicPressed;
    public Action<List<string>, string, int, Dictionary<string, float>, List<string>, List<string>>? OnCreateSessionPressed;

    public MusicPlayerWindow()
    {
        Title = Loc.GetString("admin-music-player-window-title");
        SetSize = new Vector2(860, 600);

        var leftCardStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#141416"), 
            BorderThickness = new Thickness(1),
            BorderColor = Color.FromHex("#25252a"),    
            ContentMarginLeftOverride = 8,             
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8
        };

        var horizontalLayout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true };
        var leftPanel = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 355, MaxWidth = 355, Margin = new Thickness(10), VerticalExpand = true };

        var targetsCardFrame = new PanelContainer { HorizontalExpand = true, PanelOverride = leftCardStyle, Margin = new Thickness(0, 0, 0, 10) };
        var targetsLayout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var targetsHeader = new Label { Text = Loc.GetString("admin-music-player-header-targets"), Margin = new Thickness(0, 0, 0, 6), FontColorOverride = Color.FromHex("#A0DBF5") };
        targetsLayout.AddChild(targetsHeader);

        var categoryRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 6) };
        CategorySelector = new OptionButton { HorizontalExpand = true };
        CategorySelector.AddItem(Loc.GetString("admin-music-player-category-maps"), 0);
        CategorySelector.AddItem(Loc.GetString("admin-music-player-category-players"), 1);
        
        CategorySelector.OnItemSelected += args =>
        {
            CategorySelector.SelectId(args.Id);
            OnCategoryChanged?.Invoke(args.Id);
        };
        
        categoryRow.AddChild(CategorySelector);

        RefreshTargetsButton = new Button { Text = Loc.GetString("admin-music-player-btn-refresh"), Margin = new Thickness(4, 0, 0, 0) };
        RefreshTargetsButton.OnPressed += _ => OnRefreshTargetsPressed?.Invoke();
        categoryRow.AddChild(RefreshTargetsButton);
        targetsLayout.AddChild(categoryRow);

        SearchInput = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("admin-music-player-search-placeholder"), Margin = new Thickness(0, 0, 0, 6) };
        SearchInput.OnTextChanged += args => FilterTiles(args.Text);
        targetsLayout.AddChild(SearchInput);

        TargetsScroll = new ScrollContainer { VerticalExpand = true, MinHeight = 110, MaxHeight = 110, HorizontalExpand = true };
        TargetsGrid = new GridContainer { Columns = 2, HorizontalExpand = true };
        TargetsScroll.AddChild(TargetsGrid);
        targetsLayout.AddChild(TargetsScroll);

        targetsCardFrame.AddChild(targetsLayout);
        leftPanel.AddChild(targetsCardFrame);

        leftPanel.AddChild(new Control { MinSize = new Vector2(0, 10) });

        var musicCardFrame = new PanelContainer { HorizontalExpand = true, VerticalExpand = true, PanelOverride = leftCardStyle, Margin = new Thickness(0, 0, 0, 4) };
        var musicLayout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
        var musicHeader = new Label { Text = Loc.GetString("admin-music-player-header-music"), Margin = new Thickness(0, 0, 0, 6), FontColorOverride = Color.FromHex("#A0DBF5") };
        musicLayout.AddChild(musicHeader);

        var folderRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 6) };
        FolderInput = new HistoryLineEdit { HorizontalExpand = true, PlaceHolder = "/Audio/Lobby/" };
        
        FolderInput.OnTabComplete += args =>
        {
            var currentText = FolderInput.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentText))
            {
                FolderInput.Text = "/Audio/";
                FolderInput.CursorPosition = FolderInput.Text.Length;
                _lastPrefix = "/Audio/";
                return;
            }

            if (!currentText.StartsWith("/")) currentText = "/" + currentText;

            var resManager = IoCManager.Resolve<IResourceManager>();

            if (currentText == _lastPrefix && _autocompleteSuggestions.Count > 0)
            {
                _autocompleteIndex = (_autocompleteIndex + 1) % _autocompleteSuggestions.Count;
                FolderInput.Text = _autocompleteSuggestions[_autocompleteIndex];
                FolderInput.CursorPosition = FolderInput.Text.Length;
                _lastPrefix = FolderInput.Text;
                return;
            }

            _autocompleteSuggestions.Clear();
            _autocompleteIndex = -1;

            var lastSlashIndex = currentText.LastIndexOf('/');
            var parentPathStr = currentText.Substring(0, lastSlashIndex + 1);
            var searchPrefix = currentText.Substring(lastSlashIndex + 1).ToLower();

            try
            {
                foreach (var file in resManager.ContentFindFiles(new ResPath(parentPathStr)))
                {
                    var fullPathStr = file.ToString();
                    var relativePath = fullPathStr.Substring(parentPathStr.Length);
                    var firstNextSlash = relativePath.IndexOf('/');
                    if (firstNextSlash == -1) continue;

                    var folderName = relativePath.Substring(0, firstNextSlash);
                    if (folderName.ToLower().StartsWith(searchPrefix))
                    {
                        var completeSuggestedPath = parentPathStr + folderName + "/";
                        if (!_autocompleteSuggestions.Contains(completeSuggestedPath))
                            _autocompleteSuggestions.Add(completeSuggestedPath);
                    }
                }
            }
            catch {}

            if (_autocompleteSuggestions.Count > 0)
            {
                _autocompleteIndex = 0;
                FolderInput.Text = _autocompleteSuggestions[0];
                FolderInput.CursorPosition = FolderInput.Text.Length;
                _lastPrefix = FolderInput.Text;
            }
            else
            {
                _lastPrefix = currentText;
            }
        };

        FolderInput.OnTextEntered += args => OnFolderChanged?.Invoke(args.Text ?? "");
        folderRow.AddChild(FolderInput);

        RefreshMusicButton = new Button { Text = Loc.GetString("admin-music-player-btn-refresh"), Margin = new Thickness(4, 0, 0, 0) };
        RefreshMusicButton.OnPressed += _ => OnRefreshMusicPressed?.Invoke();
        folderRow.AddChild(RefreshMusicButton);
        musicLayout.AddChild(folderRow);

        var trackScroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        TrackListContainer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        trackScroll.AddChild(TrackListContainer);
        musicLayout.AddChild(trackScroll);

        musicCardFrame.AddChild(musicLayout);
        leftPanel.AddChild(musicCardFrame);

        leftPanel.AddChild(new Control { MinSize = new Vector2(0, 10) });

        var footerParamsRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, Margin = new Thickness(0, 4, 0, 0), SeparationOverride = 8 };
        footerParamsRow.AddChild(new Label { Text = Loc.GetString("admin-music-player-volume-label"), VerticalAlignment = VAlignment.Center });
        VolumeInput = new LineEdit { Text = "0", MinWidth = 45, PlaceHolder = "0", VerticalAlignment = VAlignment.Center };
        footerParamsRow.AddChild(VolumeInput);

        CreateSessionButton = new Button { Text = Loc.GetString("admin-music-player-btn-create"), HorizontalExpand = true, MinHeight = 26, VerticalAlignment = VAlignment.Center };
        CreateSessionButton.OnPressed += _ => HandleCreateSession();
        footerParamsRow.AddChild(CreateSessionButton);
        leftPanel.AddChild(footerParamsRow);

        horizontalLayout.AddChild(leftPanel);
        var separator = new Control { MinSize = new Vector2(2, 0), Margin = new Thickness(4, 0, 4, 0) };
        horizontalLayout.AddChild(separator);

        RightPanelContainer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(10) };
        var rightHeader = new Label { Text = Loc.GetString("admin-music-player-header-sessions"), Margin = new Thickness(0, 0, 0, 6) };
        RightPanelContainer.AddChild(rightHeader);

        var cargoBlackBox = new PanelContainer { HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(0, 2, 0, 0) };
        cargoBlackBox.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#040404") };

        var rightScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        ActiveSessionsList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(8) };
        
        rightScroll.AddChild(ActiveSessionsList);
        cargoBlackBox.AddChild(rightScroll);
        RightPanelContainer.AddChild(cargoBlackBox);
        horizontalLayout.AddChild(RightPanelContainer);
        Contents.AddChild(horizontalLayout);
    }

    private void HandleCreateSession()
    {
        var selectedTracks = new List<string>();
        string fallbackPlaylistName = Loc.GetString("admin-music-player-playlist-fallback");
        string firstTrackName = fallbackPlaylistName;

        foreach (var track in _selectedTracksCache)
        {
            selectedTracks.Add(track);
            if (firstTrackName == fallbackPlaylistName) firstTrackName = track;
        }

        if (selectedTracks.Count == 0) return;

        var folderPath = string.IsNullOrWhiteSpace(FolderInput.Text) ? FolderInput.PlaceHolder : FolderInput.Text;
        if (string.IsNullOrEmpty(folderPath)) folderPath = "/Audio/Lobby/";
        if (!folderPath.EndsWith("/")) folderPath += "/";

        var fullTrackPaths = new List<string>();
        var durationsMap = new Dictionary<string, float>();
        
        var entManager = IoCManager.Resolve<IEntityManager>();
        var audioSys = entManager.System<Robust.Shared.Audio.Systems.SharedAudioSystem>();

        foreach (var track in selectedTracks)
        {
            var fullPath = folderPath + track + ".ogg";
            fullTrackPaths.Add(fullPath);

            try
            {
                var soundSpecifier = new Robust.Shared.Audio.SoundPathSpecifier(fullPath);
                var resolvedSound = audioSys.ResolveSound(soundSpecifier);
                var length = audioSys.GetAudioLength(resolvedSound);
                durationsMap[fullPath] = (float)length.TotalSeconds;
            }
            catch
            {
                durationsMap[fullPath] = 180f; 
            }
        }

        if (!int.TryParse(VolumeInput.Text, out var startVolume)) startVolume = 0;

        OnCreateSessionPressed?.Invoke(
            fullTrackPaths,
            selectedTracks.Count > 1 ? $"{firstTrackName} (+{selectedTracks.Count - 1})" : firstTrackName,
            startVolume,
            durationsMap,
            new List<string>(SelectedMapsCache),
            new List<string>(SelectedPlayersCache)
        );

        SelectedMapsCache.Clear();
        SelectedPlayersCache.Clear();
        _selectedTracksCache.Clear();
    }

    public void FilterTiles(string query)
    {
        var cleanQuery = query.Trim().ToLower();
        
        foreach (var child in TargetsGrid.Children)
        {
            if (child is Button tileButton)
            {
                if (string.IsNullOrWhiteSpace(cleanQuery))
                {
                    tileButton.Visible = true;
                    continue;
                }

                var targetText = FindLabelTextRecursive(tileButton);

                tileButton.Visible = targetText.ToLower().Contains(cleanQuery);
            }
        }
    }

    private string FindLabelTextRecursive(Control control)
    {
        if (control is Label label)
            return label.Text ?? string.Empty;

        foreach (var child in control.Children)
        {
            var foundText = FindLabelTextRecursive(child);
            if (!string.IsNullOrEmpty(foundText))
                return foundText;
        }

        return string.Empty;
    }
}




using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared._CorvaxNext.Administration.UI.Audio;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._CorvaxNext.Administration.UI.Audio;

public sealed partial class AdminAudioPanelEui : BaseEui
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    private Dictionary<Guid, string> _availablePlayers = new();

    private readonly AdminAudioPanelSystem _audioPanel;

    public AdminAudioPanelEui() : base()
    {
        IoCManager.InjectDependencies(this);

        _audioPanel = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AdminAudioPanelSystem>();

        foreach (var player in Filter.Broadcast().Recipients)
        {
            _availablePlayers.Add(player.UserId.UserId, player.Name);
        }

        _audioPanel.AudioUpdated += () => StateDirty();

        _playerManager.PlayerStatusChanged += (object? sender, SessionStatusEventArgs args) =>
        {
            switch (args.NewStatus)
            {
                case SessionStatus.InGame:
                    _availablePlayers.Add(args.Session.UserId.UserId, args.Session.Name);
                    StateDirty();
                    break;
                case SessionStatus.Disconnected:
                    _availablePlayers.Remove(args.Session.UserId.UserId);
                    StateDirty();
                    break;
            }
        };
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override AdminAudioPanelEuiState GetNewState()
    {
        return new(
            _audioPanel.Playing,
            _entityManager.GetNetEntity(_audioPanel.AudioEntity),
            _audioPanel.AudioParams.Volume,
            _audioPanel.Queue,
            _audioPanel.Global,
            _availablePlayers,
            _audioPanel.SelectedPlayers
        );
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not AdminAudioPanelEuiMessage.AdminAudioPanelEuiMessageBase)
            return;

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Fun))
        {
            Close();
            return;
        }

        switch (msg)
        {
            case AdminAudioPanelEuiMessage.Play:
                _audioPanel.Play();
                break;
            case AdminAudioPanelEuiMessage.Pause:
                _audioPanel.Pause();
                break;
            case AdminAudioPanelEuiMessage.Stop:
                _audioPanel.Stop();
                break;
            case AdminAudioPanelEuiMessage.AddTrack addTrack:
                var filename = addTrack.Filename.Trim();
                if (_resourceManager.ContentFileExists(new ResPath(filename).ToRootedPath()))
                    _audioPanel.AddToQueue(filename);
                break;
            case AdminAudioPanelEuiMessage.SetVolume setVolume:
                _audioPanel.SetVolume(setVolume.Volume);
                break;
            case AdminAudioPanelEuiMessage.SetPlaybackPosition setPlayback:
                _audioPanel.SetPlaybackPosition(setPlayback.Position);
                break;
            case AdminAudioPanelEuiMessage.SelectPlayer selectPlayer:
                _audioPanel.SelectPlayer(selectPlayer.Player);
                break;
            case AdminAudioPanelEuiMessage.UnselectPlayer unselectPlayer:
                _audioPanel.UnselectPlayer(unselectPlayer.Player);
                break;
            case AdminAudioPanelEuiMessage.GlobalToggled globalToggled:
                _audioPanel.SetGlobal(globalToggled.Toggled);
                break;
            default:
                return;
        }
        StateDirty();
    }
}

using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Corvax.AdminMusic;
using Content.Shared.Eui;

namespace Content.Server.Corvax.AdminMusic;

public sealed partial class AdminMusicEui : BaseEui
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IEntityManager _entManager = default!;

    private readonly AdminMusicSystem _music;

    public AdminMusicEui()
    {
        IoCManager.InjectDependencies(this);
        _music = _entManager.System<AdminMusicSystem>();
    }

    public override void Opened()
    {
        base.Opened();
        _music.RegisterEui(this);
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _music.UnregisterEui(this);
    }

    public override EuiStateBase GetNewState()
    {
        return _music.GetEuiState();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is CloseEuiMessage)
            return;

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Fun))
        {
            Close();
            return;
        }

        switch (msg)
        {
            case AdminMusicEuiMsg.RequestPlay request:
                _music.RequestPlay(this, request);
                break;
            case AdminMusicEuiMsg.SetPaused setPaused:
                _music.SetPaused(Player, setPaused.TrackId, setPaused.Paused);
                break;
            case AdminMusicEuiMsg.Seek seek:
                _music.Seek(Player, seek.TrackId, seek.Position);
                break;
            case AdminMusicEuiMsg.SetVolume setVolume:
                _music.SetVolume(Player, setVolume.TrackId, setVolume.Volume);
                break;
            case AdminMusicEuiMsg.Stop stop:
                _music.StopTrack(Player, stop.TrackId);
                break;
            case AdminMusicEuiMsg.Refresh:
                StateDirty();
                break;
        }
    }
}

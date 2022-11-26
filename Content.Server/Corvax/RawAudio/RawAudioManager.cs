using System.Linq;
using Content.Shared.Corvax.RawAudio;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.RawAudio;

public sealed class RawAudioManager
{
    [Dependency] private readonly IServerNetManager _net = default!;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgPlayRawAudio>();
    }

    public void PlayGlobal(byte[] data, Filter playerFilter, AudioParams? audioParams = null)
    {
        var channels = playerFilter.Recipients.Select(r => r.ConnectedClient).ToList();
        var msg = new MsgPlayRawAudio
        {
            Data = data,
        };
        _net.ServerSendToMany(msg, channels);
    }

    public void Play(byte[] data, Filter playerFilter, EntityUid uid, AudioParams? audioParams = null)
    {
        PlayGlobal(data, playerFilter, audioParams); // TODO: IMPLEMENT THIS SHIT
    }
}
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Content.Shared.Corvax.RawAudio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Network;

namespace Content.Client.Corvax.RawAudio;

public sealed class RawAudioManager
{
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IClydeAudio _clyde = default!;
    
    private readonly List<IClydeAudioSource> _playingClydeStreams = new();
    
    public void Initialize()
    {
        _net.RegisterNetMessage<MsgPlayRawAudio>(OnPlayRawAudio);
    }

    private void OnPlayRawAudio(MsgPlayRawAudio message)
    {
        if (TryCreateAudioSource(message.Data, out var source))
        {
            source.SetGlobal();
            source.StartPlaying();
            _playingClydeStreams.Add(source);
        }
    }

    private bool TryCreateAudioSource(byte[] data, [NotNullWhen(true)] out IClydeAudioSource? source)
    {
        var dataStream = new MemoryStream(data) { Position = 0 };
        var audioStream = _clyde.LoadAudioOggVorbis(dataStream);
        source = _clyde.CreateAudioSource(audioStream);
        return source != null;
    }
}

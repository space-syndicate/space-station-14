using System.Diagnostics.CodeAnalysis;
using System.IO;
using Content.Shared.Corvax.TTS;
using Robust.Client.Graphics;

namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IClydeAudio _clyde = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    
    private readonly List<IClydeAudioSource> _currentStreams = new();
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
       PlayEntity(ev.Uid, ev.Data);
    }

    private bool TryCreateAudioSource(byte[] data, [NotNullWhen(true)] out IClydeAudioSource? source)
    {
        var dataStream = new MemoryStream(data) { Position = 0 };
        var audioStream = _clyde.LoadAudioOggVorbis(dataStream);
        source = _clyde.CreateAudioSource(audioStream);
        return source != null;
    }

    private void PlayEntity(EntityUid entity, byte[] data)
    {
        if (!TryCreateAudioSource(data, out var source))
            return;

        if (!_entity.TryGetComponent<TransformComponent>(entity, out var xform))
            return;

        if (!source.SetPosition(xform.WorldPosition))
            return; // TODO: Add logging
        
        source.StartPlaying();
        _currentStreams.Add(source);
    }
}

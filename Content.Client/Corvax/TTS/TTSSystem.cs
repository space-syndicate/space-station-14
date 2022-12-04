using System.Diagnostics.CodeAnalysis;
using System.IO;
using Content.Shared.Corvax.TTS;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IClydeAudio _clyde = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedPhysicsSystem _broadPhase = default!;
    
    private ISawmill _sawmill = default!;
    private readonly HashSet<(EntityUid, IClydeAudioSource)> _currentStreams = new();
    
    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    // Little bit of duplication logic from AudioSystem
    public override void FrameUpdate(float frameTime)
    {
        var streamToRemove = new HashSet<(EntityUid, IClydeAudioSource)>();

        var ourPos = _eye.CurrentEye.Position.Position;
        foreach (var (uid, stream) in _currentStreams)
        {
            if (!stream.IsPlaying ||
                !_entity.TryGetComponent<MetaDataComponent>(uid, out var meta) ||
                Deleted(uid, meta) ||
                !_entity.TryGetComponent<TransformComponent>(uid, out var xform))
            {
                stream.Dispose();
                streamToRemove.Add((uid, stream));
                continue;
            }

            // TODO: Stop stream when entity exit PVS or just set to volume to zero?
            var mapPos = xform.MapPosition;
            if (mapPos.MapId != MapId.Nullspace)
            {
                if (!stream.SetPosition(mapPos.Position))
                {
                    _sawmill.Warning("Can't set position for audio stream, stop stream.");
                    stream.StopPlaying();
                }
            }

            if (mapPos.MapId == _eye.CurrentMap)
            {
                var collisionMask = (int) CollisionGroup.Impassable;
                var sourceRelative = ourPos - mapPos.Position;
                var occlusion = 0f;
                if (sourceRelative.Length > 0)
                {
                    occlusion = _broadPhase.IntersectRayPenetration(mapPos.MapId,
                        new CollisionRay(mapPos.Position, sourceRelative.Normalized, collisionMask),
                        sourceRelative.Length, uid);
                }
                stream.SetOcclusion(occlusion);
            }
        }

        foreach (var item in streamToRemove)
        {
            _currentStreams.Remove(item);
        }
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
       PlayEntity(ev.Uid, ev.Data);
    }

    private void PlayEntity(EntityUid uid, byte[] data)
    {
        if (!TryCreateAudioSource(data, out var source) ||
            !_entity.TryGetComponent<TransformComponent>(uid, out var xform) ||
            !source.SetPosition(xform.WorldPosition))
            return;

        source.StartPlaying();
        _currentStreams.Add((uid, source));
    }
    
    private bool TryCreateAudioSource(byte[] data, [NotNullWhen(true)] out IClydeAudioSource? source)
    {
        var dataStream = new MemoryStream(data) { Position = 0 };
        var audioStream = _clyde.LoadAudioOggVorbis(dataStream);
        source = _clyde.CreateAudioSource(audioStream);
        return source != null;
    }
}

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
    private readonly HashSet<AudioStream> _currentStreams = new();
    private readonly Dictionary<EntityUid, Queue<AudioStream>> _entityQueues = new();

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    // Little bit of duplication logic from AudioSystem
    public override void FrameUpdate(float frameTime)
    {
        var streamToRemove = new HashSet<AudioStream>();

        var ourPos = _eye.CurrentEye.Position.Position;
        foreach (var stream in _currentStreams)
        {
            if (!stream.Source.IsPlaying ||
                !_entity.TryGetComponent<MetaDataComponent>(stream.Uid, out var meta) ||
                Deleted(stream.Uid, meta) ||
                !_entity.TryGetComponent<TransformComponent>(stream.Uid, out var xform))
            {
                stream.Source.Dispose();
                streamToRemove.Add(stream);
                continue;
            }

            // TODO: Stop stream when entity exit PVS or just set to volume to zero?
            var mapPos = xform.MapPosition;
            if (mapPos.MapId != MapId.Nullspace)
            {
                if (!stream.Source.SetPosition(mapPos.Position))
                {
                    _sawmill.Warning("Can't set position for audio stream, stop stream.");
                    stream.Source.StopPlaying();
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
                        sourceRelative.Length, stream.Uid);
                }
                stream.Source.SetOcclusion(occlusion);
            }
        }

        foreach (var item in streamToRemove)
        {
            _currentStreams.Remove(item);
        }
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        if (!TryCreateAudioSource(ev.Data, out var source))
            return;

        var stream = new AudioStream(ev.Uid, source);
        AddEntityStreamToQueue(stream);
    }

    private void AddEntityStreamToQueue(AudioStream stream)
    {
        
        if (_entityQueues.TryGetValue(stream.Uid, out var queue))
        {
            queue.Enqueue(stream);
        }
        else
        {
            _entityQueues.Add(stream.Uid, new Queue<AudioStream>(new[] { stream }));
        }
    }

    private AudioStream TakeEntityStreamFromQueue(EntityUid uid)
    {
        if (_entityQueues.TryGetValue(uid, out var queue))
        {
            var stream = queue.Dequeue();
            if (queue.Count == 0)
                _entityQueues.Remove(uid);
            return stream;
        }

        throw new Exception("Stream queue was empty for that entity");
    }

    private void PlayEntity(AudioStream stream)
    {
        if (!_entity.TryGetComponent<TransformComponent>(stream.Uid, out var xform) ||
            !stream.Source.SetPosition(xform.WorldPosition))
            return;

        stream.Source.StartPlaying();
        _currentStreams.Add(stream);
    }
    
    private bool TryCreateAudioSource(byte[] data, [NotNullWhen(true)] out IClydeAudioSource? source)
    {
        var dataStream = new MemoryStream(data) { Position = 0 };
        var audioStream = _clyde.LoadAudioOggVorbis(dataStream);
        source = _clyde.CreateAudioSource(audioStream);
        return source != null;
    }

    // ReSharper disable once InconsistentNaming
    private sealed class AudioStream
    {
        public EntityUid Uid { get; }
        public IClydeAudioSource Source { get; }

        public AudioStream(EntityUid uid, IClydeAudioSource source)
        {
            Uid = uid;
            Source = source;
        }
    }
}

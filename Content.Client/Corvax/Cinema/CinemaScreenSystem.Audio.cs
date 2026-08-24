using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.Cinema;
using Robust.Client.Audio;
using Robust.Shared.Asynchronous;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;

    // Compressed segments are small and immutable. Sharing their in-flight/completed downloads prevents multiple
    // screens showing the same movie from downloading the same chunk independently.
    private readonly ConcurrentDictionary<string, Task<byte[]>> _audioSegmentDownloads = new();
    private readonly ConcurrentDictionary<string, PendingAudioSegment> _pendingAudioSegments = new();
    private readonly Queue<string> _audioSegmentCacheOrder = new();

    private sealed class PendingAudioSegment
    {
        public readonly TaskCompletionSource<byte[]> Completion = new();
        public NetEntity Entity;
        public byte[]? Data;
        public bool[]? ReceivedChunks;
        public int ReceivedCount;
    }

    private void UpdateAudio(EntityUid uid, CinemaScreenComponent comp, CinemaScreenPlayerComponent player)
    {
        if (comp.Broken ||
            !IsInPvs(uid) ||
            string.IsNullOrEmpty(comp.AudioCacheKey) ||
            comp.AudioSegmentCount <= 0 ||
            comp.AudioSegmentDuration <= 0f)
        {
            ReleaseAudio(player);
            return;
        }

        if (player.AudioCacheKey != comp.AudioCacheKey)
        {
            ReleaseAudio(player);
            player.AudioCacheKey = comp.AudioCacheKey;
        }

        var absolutePosition = Math.Max(0, CurrentPosition(comp));
        var segment = (int) Math.Floor(absolutePosition / comp.AudioSegmentDuration);
        if (segment < 0 || segment >= comp.AudioSegmentCount)
        {
            ReleaseAudio(player);
            return;
        }

        var segmentOffset = (float) (absolutePosition - segment * comp.AudioSegmentDuration);

        if (!comp.Playing)
        {
            if (player.AudioEntity is { } pausedEntity && TryComp<AudioComponent>(pausedEntity, out var pausedAudio))
            {
                if (player.AudioSegment != segment ||
                    MathF.Abs(pausedAudio.PlaybackPosition - segmentOffset) > comp.ResyncThreshold)
                {
                    StopAudioStream(player);
                }
                else
                {
                    _audio.SetState(pausedEntity, AudioState.Paused, component: pausedAudio);
                }
            }

            return;
        }

        if (player.AudioSegment != segment ||
            player.AudioEntity is not { } audioEntity ||
            !TryComp<AudioComponent>(audioEntity, out var audioComponent))
        {
            StopAudioStream(player);
            BeginLoadAudioSegment(uid, comp, player, segment, segmentOffset);
            return;
        }

        var volume = SharedAudioSystem.GainToVolume(comp.Volume);
        _audio.SetVolume(audioEntity, volume, audioComponent);

        if (audioComponent.State != AudioState.Playing)
        {
            _audio.SetState(audioEntity, AudioState.Playing, component: audioComponent);
            audioComponent.PlaybackPosition = segmentOffset;
        }
        else if (MathF.Abs(audioComponent.PlaybackPosition - segmentOffset) > comp.ResyncThreshold)
        {
            // These are local dynamic streams, so setting the OpenAL position directly avoids asking the resource
            // cache for a compile-time sound path while retaining the server clock as source of truth.
            audioComponent.PlaybackPosition = segmentOffset;
        }

        // Start fetching the next compressed chunk before the boundary. Decoding remains delayed until needed so
        // only one segment's PCM buffer per screen is resident in OpenAL.
        if (segment + 1 < comp.AudioSegmentCount)
            _ = GetAudioSegment(uid, comp.AudioCacheKey!, segment + 1);
    }

    private void BeginLoadAudioSegment(
        EntityUid uid,
        CinemaScreenComponent comp,
        CinemaScreenPlayerComponent player,
        int segment,
        float segmentOffset)
    {
        if (player.LoadingAudioSegment == segment || _timing.RealTime < player.AudioRetryAt)
            return;

        player.AudioCancellation?.Cancel();
        player.AudioCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        player.AudioCancellation = cancellation;
        player.LoadingAudioSegment = segment;

        var key = comp.AudioCacheKey!;
        _ = LoadAudioSegmentAsync(uid, key, segment, segmentOffset, cancellation.Token);
    }

    private async Task LoadAudioSegmentAsync(
        EntityUid uid,
        string key,
        int segment,
        float requestedOffset,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await GetAudioSegment(uid, key, segment).WaitAsync(cancellationToken);
            _taskManager.RunOnMainThread(() => FinishLoadAudioSegment(uid, key, segment, requestedOffset, data));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _audioSegmentDownloads.TryRemove(SegmentCacheId(key, segment), out _);
            _taskManager.RunOnMainThread(() =>
            {
                if (!TryComp<CinemaScreenPlayerComponent>(uid, out var player) || player.AudioCacheKey != key)
                    return;

                player.LoadingAudioSegment = -1;
                player.AudioRetryAt = _timing.RealTime + TimeSpan.FromSeconds(5);
                Log.Error($"Cinema audio segment download failed ({key}/{segment}): {e.Message}");
            });
        }
    }

    private void FinishLoadAudioSegment(
        EntityUid uid,
        string key,
        int segment,
        float requestedOffset,
        byte[] data)
    {
        if (!TryComp<CinemaScreenPlayerComponent>(uid, out var player) || player.AudioCacheKey != key)
            return;

        // The request is complete even if playback was paused or seeked while it was in flight. Clearing this
        // before the state checks lets the next Play/update start the correct segment instead of getting stuck.
        player.LoadingAudioSegment = -1;

        if (!TryComp<CinemaScreenComponent>(uid, out var comp) ||
            comp.AudioCacheKey != key ||
            comp.Broken ||
            !comp.Playing ||
            !IsInPvs(uid))
        {
            return;
        }

        var position = Math.Max(0, CurrentPosition(comp));
        var currentSegment = (int) Math.Floor(position / comp.AudioSegmentDuration);
        if (currentSegment != segment)
            return;

        var offset = (float) (position - currentSegment * comp.AudioSegmentDuration);
        if (!float.IsFinite(offset))
            offset = requestedOffset;

        AudioStream stream;
        try
        {
            stream = _audioManager.LoadAudioOggVorbis(
                new MemoryStream(data, writable: false),
                $"cinema-{key}-{segment:D6}.ogg");
        }
        catch (Exception e)
        {
            player.AudioRetryAt = _timing.RealTime + TimeSpan.FromSeconds(10);
            Log.Error($"Cinema audio segment decode failed ({key}/{segment}): {e.Message}");
            return;
        }

        var maxDistance = Math.Max(0.1f, comp.AudioMaxDistance);
        var audioParams = AudioParams.Default
            .WithVolume(SharedAudioSystem.GainToVolume(comp.Volume))
            .WithMaxDistance(maxDistance)
            .WithReferenceDistance(Math.Clamp(comp.AudioFullVolumeDistance, 0.1f, maxDistance))
            .WithRolloffFactor(1f)
            .WithPlayOffset(Math.Clamp(offset, 0f, Math.Max(0f, (float) stream.Length.TotalSeconds - 0.01f)));

        var result = _audio.PlayEntity(stream, uid, specifier: null, audioParams: audioParams);
        if (result == null)
        {
            stream.Dispose();
            return;
        }

        player.AudioStream = stream;
        player.AudioEntity = result.Value.Entity;
        player.AudioSegment = segment;
        Log.Info($"Cinema audio playing: entity={ToPrettyString(uid)}, segment={segment}, offset={offset:F2}s");
    }

    private Task<byte[]> GetAudioSegment(EntityUid uid, string key, int segment)
    {
        var id = SegmentCacheId(key, segment);
        if (_audioSegmentDownloads.TryGetValue(id, out var existing))
            return existing;

        var pending = new PendingAudioSegment
        {
            Entity = GetNetEntity(uid),
        };
        if (!_audioSegmentDownloads.TryAdd(id, pending.Completion.Task))
            return _audioSegmentDownloads[id];

        _pendingAudioSegments[id] = pending;
        RaiseNetworkEvent(new CinemaAudioChunkRequestEvent(pending.Entity, key, segment, 0));

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(30), () =>
        {
            if (!_pendingAudioSegments.TryRemove(id, out var pending))
                return;

            _audioSegmentDownloads.TryRemove(id, out _);
            pending.Completion.TrySetException(new InvalidOperationException("Cinema audio segment request timed out"));
        });

        return pending.Completion.Task;
    }

    private void OnAudioSegment(CinemaAudioChunkEvent message)
    {
        var id = SegmentCacheId(message.Key, message.Segment);
        if (!_pendingAudioSegments.TryGetValue(id, out var pending))
            return;

        var maxChunks = (CinemaAudioChunkEvent.MaxDataBytes + CinemaAudioChunkEvent.MaxChunkBytes - 1) /
                        CinemaAudioChunkEvent.MaxChunkBytes;
        if (message.TotalLength is <= 0 or > CinemaAudioChunkEvent.MaxDataBytes ||
            message.ChunkCount is <= 0 || message.ChunkCount > maxChunks ||
            message.ChunkIndex < 0 || message.ChunkIndex >= message.ChunkCount)
        {
            _pendingAudioSegments.TryRemove(id, out _);
            _audioSegmentDownloads.TryRemove(id, out _);
            pending.Completion.TrySetException(new InvalidDataException("Server returned invalid cinema audio chunk metadata"));
            return;
        }

        pending.Data ??= new byte[message.TotalLength];
        pending.ReceivedChunks ??= new bool[message.ChunkCount];
        if (pending.Data.Length != message.TotalLength || pending.ReceivedChunks.Length != message.ChunkCount)
            return;

        var offset = message.ChunkIndex * CinemaAudioChunkEvent.MaxChunkBytes;
        var expectedLength = Math.Min(CinemaAudioChunkEvent.MaxChunkBytes, message.TotalLength - offset);
        if (offset < 0 || expectedLength != message.Data.Length || pending.ReceivedChunks[message.ChunkIndex])
            return;

        Array.Copy(message.Data, 0, pending.Data, offset, message.Data.Length);
        pending.ReceivedChunks[message.ChunkIndex] = true;
        pending.ReceivedCount++;
        if (pending.ReceivedCount != message.ChunkCount)
        {
            var nextChunk = Array.FindIndex(pending.ReceivedChunks, received => !received);
            if (nextChunk >= 0)
            {
                // Acknowledge progress by requesting exactly one next chunk. Keeping a single chunk in flight
                // prevents cinema audio from delaying normal game traffic and network heartbeats.
                RaiseNetworkEvent(new CinemaAudioChunkRequestEvent(
                    pending.Entity,
                    message.Key,
                    message.Segment,
                    nextChunk));
            }

            return;
        }

        _pendingAudioSegments.TryRemove(id, out _);
        var data = pending.Data;
        if (data.Length < 4 ||
            data[0] != (byte) 'O' || data[1] != (byte) 'g' || data[2] != (byte) 'g' || data[3] != (byte) 'S')
        {
            _audioSegmentDownloads.TryRemove(id, out _);
            pending.Completion.TrySetException(new InvalidDataException("Server did not return a valid cinema OGG segment"));
            return;
        }

        _audioSegmentCacheOrder.Enqueue(id);
        TrimAudioSegmentCache();
        Log.Info($"Cinema audio segment assembled: segment={message.Segment}, bytes={data.Length}, chunks={message.ChunkCount}");
        pending.Completion.TrySetResult(data);
    }

    private void TrimAudioSegmentCache()
    {
        var maxSegments = Math.Max(2, _cfg.GetCVar(CinemaCVars.AudioClientCacheSegments));
        while (_audioSegmentCacheOrder.Count > maxSegments)
        {
            var oldest = _audioSegmentCacheOrder.Dequeue();
            if (_pendingAudioSegments.ContainsKey(oldest))
                continue;

            _audioSegmentDownloads.TryRemove(oldest, out _);
        }
    }

    private void ClearAudioCache()
    {
        foreach (var pending in _pendingAudioSegments.Values)
        {
            pending.Completion.TrySetCanceled();
        }

        _pendingAudioSegments.Clear();
        _audioSegmentDownloads.Clear();
        _audioSegmentCacheOrder.Clear();
    }

    private void ReleaseAudio(CinemaScreenPlayerComponent player)
    {
        player.AudioCancellation?.Cancel();
        player.AudioCancellation?.Dispose();
        player.AudioCancellation = null;
        player.LoadingAudioSegment = -1;
        player.AudioCacheKey = null;
        StopAudioStream(player);
    }

    private void StopAudioStream(CinemaScreenPlayerComponent player)
    {
        player.AudioEntity = _audio.Stop(player.AudioEntity);
        player.AudioSegment = -1;

        if (player.AudioStream is not { } stream)
            return;

        player.AudioStream = null;
        // Audio.Stop queues entity deletion. Delay buffer disposal until its OpenAL source has been released.
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(1), stream.Dispose);
    }

    private static string SegmentCacheId(string key, int segment) => $"{key}/{segment:D6}";
}

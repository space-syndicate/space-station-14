using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Content.Shared.Corvax.Cinema;

namespace Content.Client.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    private readonly ConcurrentDictionary<string, Task<byte[]>> _videoSegmentDownloads = new();
    private readonly ConcurrentDictionary<string, PendingVideoSegment> _pendingVideoSegments = new();
    private readonly Queue<string> _videoSegmentCacheOrder = new();

    private sealed class PendingVideoSegment
    {
        public readonly TaskCompletionSource<byte[]> Completion = new();
        public NetEntity Entity;
        public byte[]? Data;
        public bool[]? ReceivedChunks;
        public int ReceivedCount;
    }

    private Task<byte[]> GetVideoSegment(EntityUid uid, string key, int segment)
    {
        var id = SegmentCacheId(key, segment);
        if (_videoSegmentDownloads.TryGetValue(id, out var existing))
            return existing;

        var pending = new PendingVideoSegment
        {
            Entity = GetNetEntity(uid),
        };
        if (!_videoSegmentDownloads.TryAdd(id, pending.Completion.Task))
            return _videoSegmentDownloads[id];

        _pendingVideoSegments[id] = pending;
        _ = ObserveVideoDownload(pending.Completion.Task);
        RaiseNetworkEvent(new CinemaVideoChunkRequestEvent(pending.Entity, key, segment, 0));

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(30), () =>
        {
            if (!_pendingVideoSegments.TryGetValue(id, out var current) || current != pending)
                return;

            _pendingVideoSegments.TryRemove(id, out _);
            _videoSegmentDownloads.TryRemove(id, out _);
            pending.Completion.TrySetException(new InvalidOperationException("Cinema video segment request timed out"));
        });

        return pending.Completion.Task;
    }

    private async Task ObserveVideoDownload(Task<byte[]> task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Warning($"Cinema video download failed: {e.Message}");
        }
    }

    private void OnVideoSegment(CinemaVideoChunkEvent message)
    {
        var id = SegmentCacheId(message.Key, message.Segment);
        if (!_pendingVideoSegments.TryGetValue(id, out var pending))
            return;

        var maxChunks = (CinemaVideoChunkEvent.MaxDataBytes + CinemaVideoChunkEvent.MaxChunkBytes - 1) /
                        CinemaVideoChunkEvent.MaxChunkBytes;
        if (message.TotalLength is <= 0 or > CinemaVideoChunkEvent.MaxDataBytes ||
            message.ChunkCount is <= 0 || message.ChunkCount > maxChunks ||
            message.ChunkCount != (message.TotalLength + CinemaVideoChunkEvent.MaxChunkBytes - 1) / CinemaVideoChunkEvent.MaxChunkBytes ||
            message.ChunkIndex < 0 || message.ChunkIndex >= message.ChunkCount)
        {
            _pendingVideoSegments.TryRemove(id, out _);
            _videoSegmentDownloads.TryRemove(id, out _);
            pending.Completion.TrySetException(new InvalidDataException("Server returned invalid cinema video chunk metadata"));
            return;
        }

        pending.Data ??= new byte[message.TotalLength];
        pending.ReceivedChunks ??= new bool[message.ChunkCount];
        if (pending.Data.Length != message.TotalLength || pending.ReceivedChunks.Length != message.ChunkCount)
            return;

        var offset = message.ChunkIndex * CinemaVideoChunkEvent.MaxChunkBytes;
        var expectedLength = Math.Min(CinemaVideoChunkEvent.MaxChunkBytes, message.TotalLength - offset);
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
                // prevents cinema video from delaying normal game traffic and network heartbeats.
                RaiseNetworkEvent(new CinemaVideoChunkRequestEvent(
                    pending.Entity,
                    message.Key,
                    message.Segment,
                    nextChunk));
            }

            return;
        }

        _pendingVideoSegments.TryRemove(id, out _);
        var data = pending.Data;
        if (data.Length < 4 ||
            data[0] != 0x1a || data[1] != 0x45 || data[2] != 0xdf || data[3] != 0xa3)
        {
            _videoSegmentDownloads.TryRemove(id, out _);
            pending.Completion.TrySetException(new InvalidDataException("Server did not return a valid cinema WebM segment"));
            return;
        }

        _videoSegmentCacheOrder.Enqueue(id);
        TrimVideoSegmentCache();
        Log.Debug($"Cinema video segment assembled: segment={message.Segment}, bytes={data.Length}, chunks={message.ChunkCount}");
        pending.Completion.TrySetResult(data);
    }

    private void TrimVideoSegmentCache()
    {
        var maxSegments = 4;
        while (_videoSegmentCacheOrder.Count > maxSegments)
        {
            var oldest = _videoSegmentCacheOrder.Dequeue();
            if (_pendingVideoSegments.ContainsKey(oldest))
                continue;

            _videoSegmentDownloads.TryRemove(oldest, out _);
        }
    }

    private void ClearVideoCache()
    {
        foreach (var pending in _pendingVideoSegments.Values)
        {
            pending.Completion.TrySetCanceled();
        }

        _pendingVideoSegments.Clear();
        _videoSegmentDownloads.Clear();
        _videoSegmentCacheOrder.Clear();
    }

    private string? GetVideoSource(EntityUid uid, CinemaScreenComponent comp, CinemaScreenPlayerComponent player, ref double time)
    {
        if (player.WebView == null || player.WebView.IsLoading)
        {
            player.VideoSegmentId = null;
            player.ReadyVideoSegmentId = null;
            return null;
        }
        if (!comp.HasVideoSegments || comp.AudioCacheKey is not { } key || !IsInPvs(uid))
            return null;
        var segment = (int) Math.Floor(Math.Max(0, time) / comp.AudioSegmentDuration);
        if (segment >= comp.AudioSegmentCount)
            return null;
        time -= segment * comp.AudioSegmentDuration;
        var id = SegmentCacheId(key, segment);
        if (player.VideoSegmentId == id && player.ReadyVideoSegmentId != id &&
            _timing.RealTime - player.VideoLoadSentAt > TimeSpan.FromSeconds(5))
            player.VideoSegmentId = null; // A script sent before page initialization must be retried.
        if (player.VideoSegmentId != id)
        {
            var task = GetVideoSegment(uid, key, segment);
            if (!task.IsCompletedSuccessfully)
            {
                return null;
            }
            // IsCompletedSuccessfully above guarantees this read cannot block the game thread.
#pragma warning disable RA0004
            var data = task.Result;
#pragma warning restore RA0004
            player.ReadyVideoSegmentId = null;
            player.WebView?.ExecuteJavaScript("cinema.loadSegment(" +
                                             JsString(Convert.ToBase64String(data)) + ");");
            player.VideoSegmentId = id;
            player.VideoLoadSentAt = _timing.RealTime;
        }
        if (segment + 1 < comp.AudioSegmentCount)
            _ = GetVideoSegment(uid, key, segment + 1);
        return "segment:" + id;
    }
}

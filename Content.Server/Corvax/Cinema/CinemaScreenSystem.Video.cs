using System.IO;
using System.Linq;
using Content.Shared.Corvax.Cinema;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    private void OnVideoRequest(CinemaVideoChunkRequestEvent message, EntitySessionEventArgs args)
    {
        byte[] chunk = [];
        var chunkCount = 0;
        var totalLength = 0;

        if (message.Key.Length == 64 &&
            message.Key.All(Uri.IsHexDigit) &&
            message.Segment >= 0 &&
            message.ChunkIndex >= 0 &&
            TryGetEntity(message.Entity, out var uid) &&
            uid is { } screenUid &&
            TryComp<CinemaScreenComponent>(screenUid, out var screen) &&
            screen.HasVideoSegments && screen.AudioCacheKey == message.Key &&
            message.Segment < screen.AudioSegmentCount)
        {
            var path = Path.Combine(_audioCacheRoot, message.Key, $"video-{message.Segment:D6}.webm");
            if (File.Exists(path))
            {
                var fileLength = new FileInfo(path).Length;
                if (fileLength is > 0 and <= CinemaVideoChunkEvent.MaxDataBytes)
                {
                    totalLength = (int) fileLength;
                    chunkCount = (totalLength + CinemaVideoChunkEvent.MaxChunkBytes - 1) /
                                 CinemaVideoChunkEvent.MaxChunkBytes;

                    if (message.ChunkIndex < chunkCount)
                    {
                        var offset = message.ChunkIndex * CinemaVideoChunkEvent.MaxChunkBytes;
                        var length = Math.Min(CinemaVideoChunkEvent.MaxChunkBytes, totalLength - offset);
                        chunk = new byte[length];
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        stream.Position = offset;
                        stream.ReadExactly(chunk);
                    }
                }
            }
        }

        if (chunk.Length == 0)
        {
            RaiseNetworkEvent(
                new CinemaVideoChunkEvent(message.Key, message.Segment),
                Filter.SinglePlayer(args.SenderSession),
                recordReplay: false);
            return;
        }

        if (message.ChunkIndex == 0)
            Log.Debug($"Cinema video sending: segment={message.Segment}, bytes={totalLength}, chunks={chunkCount}, paced=true");

        // Send only one chunk per request. The client asks for the next chunk after receiving this one,
        // which keeps large WebM segments from flooding the game channel and starving its heartbeat.
        RaiseNetworkEvent(
            new CinemaVideoChunkEvent(
                message.Key,
                message.Segment,
                message.ChunkIndex,
                chunkCount,
                totalLength,
                chunk),
            Filter.SinglePlayer(args.SenderSession),
            recordReplay: false);
    }

}

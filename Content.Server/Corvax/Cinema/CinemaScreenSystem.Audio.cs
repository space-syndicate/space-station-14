using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.Cinema;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    private const string AudioEncodingVersion = "vorbis-mono-segments-v2";

    [Dependency] private ITaskManager _taskManager = default!;

    private readonly HttpClient _audioHttp = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
    });

    private readonly ConcurrentDictionary<string, Task<AudioManifest?>> _audioJobs = new();
    private readonly Dictionary<EntityUid, string> _entityAudioRequests = new();
    private readonly SemaphoreSlim _audioExtractionGate = new(1, 1);
    private CancellationTokenSource _audioCancellation = new();
    private readonly List<string> _audioCacheKeys = new();
    private int _audioCacheGeneration;
    private readonly string _audioCacheRoot = Path.Combine(Path.GetTempPath(), "covax-cinema-audio");

    private sealed record AudioManifest(string Key, int SegmentCount, float SegmentDuration);

    private void InitializeAudioExtraction()
    {
        DeleteAudioCacheDirectory();
        Directory.CreateDirectory(_audioCacheRoot);
    }

    private void ShutdownAudioExtraction()
    {
        _audioCancellation.Cancel();
        _audioCancellation.Dispose();
        _audioHttp.Dispose();
        DeleteAudioCacheDirectory();
    }

    private void ResetAudioExtractionCache()
    {
        _audioCacheGeneration++;
        _audioCancellation.Cancel();
        _audioCancellation.Dispose();
        _audioCancellation = new CancellationTokenSource();
        _audioJobs.Clear();
        _entityAudioRequests.Clear();
        _audioCacheKeys.Clear();
        DeleteAudioCacheDirectory();
        Directory.CreateDirectory(_audioCacheRoot);

        var query = EntityQueryEnumerator<CinemaScreenComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            ResetAudioMetadata(comp);
            Dirty(uid, comp);
        }
    }

    private static void ResetAudioMetadata(CinemaScreenComponent comp)
    {
        comp.AudioCacheKey = null;
        comp.AudioSegmentCount = 0;
    }

    private void PrepareAudio(EntityUid uid, CinemaScreenComponent comp)
    {
        if (!_cfg.GetCVar(CinemaCVars.AudioExtractionEnabled) ||
            comp.VideoUrl is not { } url ||
            !IsDirectMediaUrl(url))
        {
            return;
        }

        var segmentSeconds = Math.Clamp(_cfg.GetCVar(CinemaCVars.AudioSegmentSeconds), 5, 120);
        var key = CreateAudioKey(url, segmentSeconds);

        if (comp.AudioCacheKey == key && comp.AudioSegmentCount > 0)
            return;

        if (_entityAudioRequests.TryGetValue(uid, out var pendingKey) && pendingKey == key)
            return;

        _entityAudioRequests[uid] = key;
        var generation = _audioCacheGeneration;
        Log.Info($"Cinema audio queued: key={key}, source='{url}'");
        var job = _audioJobs.GetOrAdd(key, _ => ExtractAudioAsync(key, url, segmentSeconds, _audioCancellation.Token));
        _ = FinishAudioPreparation(uid, url, key, generation, job);
    }

    private async Task FinishAudioPreparation(
        EntityUid uid,
        string url,
        string key,
        int generation,
        Task<AudioManifest?> job)
    {
        AudioManifest? manifest = null;
        try
        {
            manifest = await job.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            Log.Error($"Cinema audio extraction failed for '{url}': {e}");
            _audioJobs.TryRemove(key, out _);
        }

        _taskManager.RunOnMainThread(() =>
        {
            if (_entityAudioRequests.TryGetValue(uid, out var requested) && requested == key)
                _entityAudioRequests.Remove(uid);

            if (manifest == null ||
                generation != _audioCacheGeneration ||
                EntityManager.ShuttingDown ||
                !TryComp<CinemaScreenComponent>(uid, out var current) ||
                current.Broken ||
                current.VideoUrl != url)
            {
                return;
            }

            current.AudioCacheKey = manifest.Key;
            current.AudioSegmentCount = manifest.SegmentCount;
            current.AudioSegmentDuration = manifest.SegmentDuration;
            Dirty(uid, current);
            TrackAndTrimAudioCache(manifest.Key);
            Log.Info($"Cinema audio ready: key={manifest.Key}, segments={manifest.SegmentCount}, source='{url}'");
        });
    }

    private async Task<AudioManifest?> ExtractAudioAsync(
        string key,
        string url,
        int segmentSeconds,
        CancellationToken shutdownToken)
    {
        // Keep download, disk and CPU pressure bounded even if several admins set different films at once.
        await _audioExtractionGate.WaitAsync(shutdownToken).ConfigureAwait(false);
        try
        {
            return await ExtractAudioCoreAsync(key, url, segmentSeconds, shutdownToken).ConfigureAwait(false);
        }
        finally
        {
            _audioExtractionGate.Release();
        }
    }

    private async Task<AudioManifest?> ExtractAudioCoreAsync(
        string key,
        string url,
        int segmentSeconds,
        CancellationToken shutdownToken)
    {
        var finalDir = Path.Combine(_audioCacheRoot, key);
        var cachedCount = CountSegments(finalDir);
        if (cachedCount > 0)
            return new AudioManifest(key, cachedCount, segmentSeconds);

        var timeout = TimeSpan.FromSeconds(Math.Clamp(
            _cfg.GetCVar(CinemaCVars.AudioExtractionTimeoutSeconds), 30, 7200));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        timeoutCts.CancelAfter(timeout);
        var cancellationToken = timeoutCts.Token;

        var workDir = Path.Combine(_audioCacheRoot, $".{key}.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var inputPath = Path.Combine(workDir, "source.media");

        try
        {
            var maxBytes = Math.Max(1, _cfg.GetCVar(CinemaCVars.AudioMaxInputMiB)) * 1024L * 1024L;
            Log.Info($"Cinema audio download started: source='{url}'");
            await DownloadMediaAsync(new Uri(url), inputPath, maxBytes, cancellationToken).ConfigureAwait(false);

            var outputPattern = Path.Combine(workDir, "raw-segment-%06d.ogg");
            var ffmpegPath = _cfg.GetCVar(CinemaCVars.AudioFfmpegPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-nostdin");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:a:0");
            startInfo.ArgumentList.Add("-vn");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("libvorbis");
            startInfo.ArgumentList.Add("-q:a");
            startInfo.ArgumentList.Add("3");
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add("48000");
            startInfo.ArgumentList.Add("-ac");
            // Positional OpenAL attenuation only works correctly for mono sources.
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("segment");
            startInfo.ArgumentList.Add("-segment_time");
            startInfo.ArgumentList.Add(segmentSeconds.ToString());
            startInfo.ArgumentList.Add("-reset_timestamps");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add(outputPattern);

            using var process = new Process { StartInfo = startInfo };
            Log.Info($"Cinema audio ffmpeg started: key={key}");
            if (!process.Start())
                throw new InvalidOperationException($"Unable to start ffmpeg at '{ffmpegPath}'");

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                throw;
            }

            var stderr = await stderrTask.ConfigureAwait(false);
            _ = await stdoutTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {LastLine(stderr)}");

            await NormalizeOggSegmentsAsync(workDir, ffmpegPath, cancellationToken).ConfigureAwait(false);
            File.Delete(inputPath);
            var segmentCount = CountSegments(workDir);
            if (segmentCount == 0)
                throw new InvalidOperationException("ffmpeg produced no OGG segments (the source may not contain audio)");

            if (Directory.Exists(finalDir))
            {
                Directory.Delete(workDir, recursive: true);
                segmentCount = CountSegments(finalDir);
            }
            else
            {
                Directory.Move(workDir, finalDir);
            }

            return segmentCount > 0 ? new AudioManifest(key, segmentCount, segmentSeconds) : null;
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    private static async Task NormalizeOggSegmentsAsync(
        string workDir,
        string ffmpegPath,
        CancellationToken cancellationToken)
    {
        var rawSegments = Directory
            .EnumerateFiles(workDir, "raw-segment-*.ogg", SearchOption.TopDirectoryOnly)
            .Order()
            .ToArray();
        if (rawSegments.Length == 0)
            throw new InvalidOperationException("ffmpeg produced no raw OGG segments");

        // The segment muxer produces valid OGG files, but their page/granule index is rejected by the
        // VorbisPizza decoder used by Robust. A stream-copy remux rebuilds that index without decoding or
        // re-encoding audio, so this adds negligible CPU load and preserves the encoded samples.
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-y");
        foreach (var rawSegment in rawSegments)
        {
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(rawSegment);
        }

        for (var i = 0; i < rawSegments.Length; i++)
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add($"{i}:a:0");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("copy");
            startInfo.ArgumentList.Add(Path.Combine(workDir, $"segment-{i:D6}.ogg"));
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Unable to start ffmpeg remux at '{ffmpegPath}'");

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        var stderr = await stderrTask.ConfigureAwait(false);
        _ = await stdoutTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg OGG remux exited with code {process.ExitCode}: {LastLine(stderr)}");

        foreach (var rawSegment in rawSegments)
        {
            File.Delete(rawSegment);
        }
    }

    private async Task DownloadMediaAsync(Uri initialUri, string destination, long maxBytes, CancellationToken cancellationToken)
    {
        var uri = initialUri;
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            if (!IsUrlAllowed(uri.ToString()) || !IsDirectMediaUrl(uri.ToString()))
                throw new InvalidOperationException($"Media redirect is not an allowed direct MP4/WebM URL: {uri}");

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            // Wikimedia and some other media hosts reject the default empty .NET user agent with HTTP 403.
            request.Headers.UserAgent.ParseAdd("Corvax-Cinema/1.0");
            using var response = await _audioHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if ((int) response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
            {
                uri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(uri, response.Headers.Location);
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length > maxBytes)
                throw new InvalidOperationException($"Video is larger than the configured {maxBytes / 1024 / 1024} MiB limit");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                useAsync: true);

            var buffer = new byte[1024 * 128];
            long total = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                total += read;
                if (total > maxBytes)
                    throw new InvalidOperationException($"Video exceeded the configured {maxBytes / 1024 / 1024} MiB limit");

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        throw new InvalidOperationException("Too many redirects while downloading cinema media");
    }

    private void OnAudioRequest(CinemaAudioChunkRequestEvent message, EntitySessionEventArgs args)
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
            screen.AudioCacheKey == message.Key &&
            message.Segment < screen.AudioSegmentCount)
        {
            var path = Path.Combine(_audioCacheRoot, message.Key, $"segment-{message.Segment:D6}.ogg");
            if (File.Exists(path))
            {
                var fileLength = new FileInfo(path).Length;
                if (fileLength is > 0 and <= CinemaAudioChunkEvent.MaxDataBytes)
                {
                    totalLength = (int) fileLength;
                    chunkCount = (totalLength + CinemaAudioChunkEvent.MaxChunkBytes - 1) /
                                 CinemaAudioChunkEvent.MaxChunkBytes;

                    if (message.ChunkIndex < chunkCount)
                    {
                        var offset = message.ChunkIndex * CinemaAudioChunkEvent.MaxChunkBytes;
                        var length = Math.Min(CinemaAudioChunkEvent.MaxChunkBytes, totalLength - offset);
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
                new CinemaAudioChunkEvent(message.Key, message.Segment),
                Filter.SinglePlayer(args.SenderSession),
                recordReplay: false);
            return;
        }

        if (message.ChunkIndex == 0)
            Log.Info($"Cinema audio sending: segment={message.Segment}, bytes={totalLength}, chunks={chunkCount}, paced=true");

        // Send only one chunk per request. The client asks for the next chunk after receiving this one,
        // which keeps large OGG segments from flooding the game channel and starving its heartbeat.
        RaiseNetworkEvent(
            new CinemaAudioChunkEvent(
                message.Key,
                message.Segment,
                message.ChunkIndex,
                chunkCount,
                totalLength,
                chunk),
            Filter.SinglePlayer(args.SenderSession),
            recordReplay: false);
    }

    private static bool IsDirectMediaUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
               uri.AbsolutePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateAudioKey(string url, int segmentSeconds)
    {
        var input = Encoding.UTF8.GetBytes($"{AudioEncodingVersion}\n{segmentSeconds}\n{url}");
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }

    private static int CountSegments(string directory)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "segment-*.ogg", SearchOption.TopDirectoryOnly).Count()
            : 0;
    }

    private static string LastLine(string text)
    {
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unknown error";
    }

    private void TrackAndTrimAudioCache(string key)
    {
        _audioCacheKeys.Remove(key);
        _audioCacheKeys.Add(key);

        var maxTracks = Math.Max(1, _cfg.GetCVar(CinemaCVars.AudioMaxCachedTracks));
        if (_audioCacheKeys.Count <= maxTracks)
            return;

        var activeKeys = new HashSet<string>();
        var query = EntityQueryEnumerator<CinemaScreenComponent>();
        while (query.MoveNext(out var screen))
        {
            if (screen.AudioCacheKey is { } activeKey)
                activeKeys.Add(activeKey);
        }

        for (var i = 0; i < _audioCacheKeys.Count && _audioCacheKeys.Count > maxTracks;)
        {
            var candidate = _audioCacheKeys[i];
            if (activeKeys.Contains(candidate))
            {
                i++;
                continue;
            }

            _audioCacheKeys.RemoveAt(i);
            _audioJobs.TryRemove(candidate, out _);
            var directory = Path.Combine(_audioCacheRoot, candidate);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private void DeleteAudioCacheDirectory()
    {
        if (!Directory.Exists(_audioCacheRoot))
            return;

        try
        {
            Directory.Delete(_audioCacheRoot, recursive: true);
        }
        catch (IOException e)
        {
            Log.Warning($"Unable to fully clean cinema audio cache: {e.Message}");
        }
    }
}

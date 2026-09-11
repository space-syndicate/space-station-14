using System.Collections.Concurrent;
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
    private const string AudioEncodingVersion = "vorbis-vp8-stream-v5";

    [Dependency] private ITaskManager _taskManager = default!;

    private readonly HttpClient _audioHttp = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
    });

    private readonly ConcurrentDictionary<string, Task<AudioManifest?>> _audioJobs = new();
    private readonly Dictionary<EntityUid, string> _entityAudioRequests = new();
    private readonly Dictionary<string, CancellationTokenSource> _audioJobCancellation = new();
    private readonly Dictionary<Task<AudioManifest?>, string> _retiredAudioJobs = new();
    private readonly SemaphoreSlim _audioExtractionGate = new(1, 1);
    private CancellationTokenSource _audioCancellation = new();
    private readonly List<string> _audioCacheKeys = new();
    private int _audioCacheGeneration;
    private string _audioCacheRoot = NewAudioCacheDirectory();

    private static string NewAudioCacheDirectory() =>
        CinemaCacheDirectory.NewPath(Path.GetTempPath());

    internal sealed record AudioManifest(string Key, int SegmentCount, float SegmentDuration, bool HasVideo = false, double Duration = 0);

    private void InitializeAudioExtraction()
    {
        CinemaCacheDirectory.RemoveStale(Path.GetTempPath(), message => Log.Warning(message));
        Directory.CreateDirectory(_audioCacheRoot);
    }

    private void ShutdownAudioExtraction()
    {
        _audioCancellation.Cancel();
        _audioCancellation.Dispose();
        _audioHttp.Dispose();
        _ = DeleteAudioCacheAfterJobsAsync(_audioCacheRoot, _audioJobs.Values.Concat(_retiredAudioJobs.Keys).ToArray());
    }

    private void ResetAudioExtractionCache()
    {
        _audioCacheGeneration++;
        _preparationProgress = new();
        _streamingProgress = new();
        _episodeRequests.Clear();
        _audioCancellation.Cancel();
        _audioCancellation.Dispose();
        _audioCancellation = new CancellationTokenSource();
        var oldJobs = _audioJobs.Values.Concat(_retiredAudioJobs.Keys).ToArray();
        _audioJobs.Clear();
        _audioJobCancellation.Clear();
        _retiredAudioJobs.Clear();
        _entityAudioRequests.Clear();
        _audioCacheKeys.Clear();
        var oldRoot = _audioCacheRoot;
        _ = DeleteAudioCacheAfterJobsAsync(oldRoot, oldJobs);
        _audioCacheRoot = NewAudioCacheDirectory();
        Directory.CreateDirectory(_audioCacheRoot);

        var query = EntityQueryEnumerator<CinemaScreenComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            ResetAudioMetadata(comp);
            UpdateScreen(uid, comp);
        }
    }

    private static void ResetAudioMetadata(CinemaScreenComponent comp)
    {
        comp.PlayWhenPrepared = false;
        comp.StreamPreparing = false;
        comp.StreamFailed = false;
        comp.Buffering = false;
        comp.StreamDuration = 0;
        comp.AudioCacheKey = null;
        comp.HasVideoSegments = false;
        comp.AudioSegmentCount = 0;
        comp.AudioStatus = "cinema-status-empty";
        comp.PreparationStage = null;
        comp.PreparationPercent = -1;
    }

    private void PrepareAudio(EntityUid uid, CinemaScreenComponent comp)
    {
        if (comp.Broken || comp.VideoUrl is not { } url || !IsUrlAllowed(url))
            return;

        if (TryGetAniLibertyEpisode(url, out _) && comp.ResolvedVideoUrl == null)
        {
            PrepareAniLiberty(uid, comp, url);
            return;
        }

        url = comp.ResolvedVideoUrl ?? url;
        if (!_cfg.GetCVar(CinemaCVars.AudioExtractionEnabled) && !IsAniLibertyMedia(new Uri(url), ".m3u8"))
        {
            comp.AudioStatus = "cinema-status-disabled";
            UpdateScreen(uid, comp);
            return;
        }

        var segmentSeconds = Math.Clamp(_cfg.GetCVar(CinemaCVars.AudioSegmentSeconds), 5, 120);
        var key = CreateAudioKey(url, segmentSeconds);

        if (comp.AudioCacheKey == key && comp.AudioSegmentCount > 0 && !comp.StreamFailed)
            return;

        if (_entityAudioRequests.TryGetValue(uid, out var pendingKey) && pendingKey == key)
            return;

        if (comp.Playing)
        {
            comp.PausePosition = CurrentPosition(comp);
            comp.PlayWhenPrepared = true;
            comp.Playing = false;
        }
        if (comp.StreamFailed)
        {
            comp.AudioCacheKey = null;
            comp.AudioSegmentCount = 0;
            comp.HasVideoSegments = false;
        }
        comp.StreamFailed = false;
        comp.StreamPreparing = true;
        comp.HasVideoSegments = true;
        _entityAudioRequests[uid] = key;
        TrackAndTrimAudioCache(key);
        comp.AudioStatus = "cinema-status-loading";
        comp.PreparationStage = "cinema-stage-queued";
        comp.PreparationPercent = -1;
        UpdateScreen(uid, comp);
        var generation = _audioCacheGeneration;
        Log.Debug($"Cinema audio queued: key={key}, source='{url}'");
        var root = _audioCacheRoot;
        var job = GetOrStartAudioJob(uid, key, token => ExtractAudioAsync(key, url, segmentSeconds, root, token));
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
            // Shutdown/round changes are ignored below; a worker timeout is reported to the controls.
        }
        catch (Exception e)
        {
            Log.Error($"Cinema audio extraction failed for '{url}': {e}");
        }

        _taskManager.RunOnMainThread(() =>
        {
            if (generation != _audioCacheGeneration || EntityManager.ShuttingDown || _retiredAudioJobs.ContainsKey(job))
                return;

            if (_audioJobs.TryGetValue(key, out var activeJob) && activeJob != job)
                return;
            _audioJobCancellation.Remove(key);
            if (manifest == null)
            {
                if (_audioJobs.TryGetValue(key, out var currentJob) && currentJob == job)
                    _audioJobs.TryRemove(key, out _);
            }
            else
            {
                // Include completed jobs even when their screen has since selected another film.
                TrackAndTrimAudioCache(manifest.Key);
            }

            _preparationProgress.TryRemove(key, out _);
            _streamingProgress.TryRemove(key, out _);
            if (!_entityAudioRequests.TryGetValue(uid, out var requested) || requested != key)
                return;

            _entityAudioRequests.Remove(uid);
            if (!TryComp<CinemaScreenComponent>(uid, out var current) ||
                current.Broken || (current.ResolvedVideoUrl ?? current.VideoUrl) != url)
                return;

            current.PreparationStage = null;
            current.PreparationPercent = -1;
            if (manifest == null)
            {
                current.PlayWhenPrepared = false;
                if (current.Playing)
                    current.PausePosition = CurrentPosition(current);
                current.Playing = false;
                current.StreamPreparing = false;
                current.StreamFailed = true;
                current.Buffering = false;
                current.AudioStatus = "cinema-status-error";
                UpdateScreen(uid, current);
                return;
            }

            ApplyPreparedMedia(uid, current, manifest);
            Log.Debug($"Cinema audio ready: key={manifest.Key}, segments={manifest.SegmentCount}, source='{url}'");
        });
    }

    internal void ApplyPreparedMedia(EntityUid uid, CinemaScreenComponent current, AudioManifest manifest)
    {
        if (manifest.HasVideo && !current.HasVideoSegments && current.Playing)
            current.ServerStartTime = _timing.RealTime;
        current.StreamPreparing = false;
        current.StreamFailed = false;
        current.Buffering = false;
        current.StreamDuration = manifest.Duration;
        if (manifest.Duration > 0)
            current.PausePosition = Math.Min(current.PausePosition, Math.Max(0, manifest.Duration - 0.05));
        current.HasVideoSegments = manifest.HasVideo;
        current.AudioPlaybackEnabled = _cfg.GetCVar(CinemaCVars.AudioExtractionEnabled);
        current.AudioStatus = "cinema-status-paused";
        current.AudioCacheKey = manifest.Key;
        current.AudioSegmentCount = manifest.SegmentCount;
        current.AudioSegmentDuration = manifest.SegmentDuration;
        if (current.PlayWhenPrepared)
        {
            current.PlayWhenPrepared = false;
            current.ServerStartTime = _timing.RealTime;
            current.Playing = true;
        }
        UpdateScreen(uid, current);
    }

    private async Task<AudioManifest?> ExtractAudioAsync(
        string key,
        string url,
        int segmentSeconds,
        string cacheRoot,
        CancellationToken shutdownToken)
    {
        var progressStore = _preparationProgress;
        var streamStore = _streamingProgress;
        void Report(string stage, int percent) => progressStore[key] = new PreparationProgress(stage, percent);
        Report("cinema-stage-queued", -1);
        // Keep download, disk and CPU pressure bounded even if several admins set different films at once.
        await _audioExtractionGate.WaitAsync(shutdownToken).ConfigureAwait(false);
        try
        {
            if (IsAniLibertyMedia(new Uri(url), ".m3u8"))
                return await StreamAniLibertyAsync(key, new Uri(url), segmentSeconds, cacheRoot, Report,
                    value => streamStore[key] = value, shutdownToken).ConfigureAwait(false);
            return await StreamDirectMediaAsync(key, url, segmentSeconds, cacheRoot, Report,
                value => streamStore[key] = value, shutdownToken).ConfigureAwait(false);
        }
        finally
        {
            _audioExtractionGate.Release();
        }
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
            Log.Debug($"Cinema audio sending: segment={message.Segment}, bytes={totalLength}, chunks={chunkCount}, paced=true");

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

    private void TrackAndTrimAudioCache(string key)
    {
        _audioCacheKeys.Remove(key);
        _audioCacheKeys.Add(key);

        var maxTracks = Math.Max(1, _cfg.GetCVar(CinemaCVars.AudioMaxCachedTracks));
        if (_audioCacheKeys.Count <= maxTracks)
            return;

        var activeKeys = new HashSet<string>(_entityAudioRequests.Values);
        foreach (var (jobKey, job) in _audioJobs)
        {
            if (!job.IsCompleted)
                activeKeys.Add(jobKey);
        }
        foreach (var (job, jobKey) in _retiredAudioJobs)
        {
            if (!job.IsCompleted)
                activeKeys.Add(jobKey);
        }
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

    private async Task DeleteAudioCacheAfterJobsAsync(string directory, Task<AudioManifest?>[] jobs)
    {
        try
        {
            await Task.WhenAll(jobs).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Worker failures are reported by FinishAudioPreparation.
        }
        DeleteAudioCacheDirectory(directory);
    }

    private void DeleteAudioCacheDirectory(string? directory = null)
    {
        directory ??= _audioCacheRoot;
        if (!Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException e)
        {
            Log.Warning($"Unable to fully clean cinema audio cache: {e.Message}");
        }
    }
}

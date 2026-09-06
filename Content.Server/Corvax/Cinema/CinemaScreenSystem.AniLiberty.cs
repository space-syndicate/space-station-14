using System.IO;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.Cinema;

namespace Content.Server.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    private readonly Dictionary<EntityUid, object> _episodeRequests = new();

    public static bool TryGetAniLibertyEpisode(string url, out Guid episode)
    {
        episode = default;
        const string prefix = "/anime/video/episode/";
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
               (uri.Host == "aniliberty.top" || uri.Host == "www.aniliberty.top") &&
               uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) &&
               Guid.TryParseExact(uri.AbsolutePath[prefix.Length..].TrimEnd('/'), "D", out episode);
    }

    private static bool IsAniLibertyMedia(Uri uri, string extension)
    {
        return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
               (uri.Host == "libria.fun" || uri.Host.EndsWith(".libria.fun", StringComparison.Ordinal)) &&
               uri.AbsolutePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }

    private void PrepareAniLiberty(EntityUid uid, CinemaScreenComponent comp, string url)
    {
        if (_episodeRequests.ContainsKey(uid))
            return;

        var request = new object();
        _episodeRequests[uid] = request;
        comp.AudioStatus = "cinema-status-resolving";
        UpdateScreen(uid, comp);
        _ = ResolveAniLibertyAsync(uid, comp, url, request, _audioCancellation.Token);
    }

    private async Task ResolveAniLibertyAsync(
        EntityUid uid, CinemaScreenComponent original, string url, object request, CancellationToken shutdown)
    {
        string? stream = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            if (!TryGetAniLibertyEpisode(url, out var episode))
                return;

            var api = new Uri($"https://aniliberty.top/api/v1/anime/releases/episodes/{episode:D}");
            var bytes = await GetAniLibertyBytesAsync(api, 1024 * 1024, timeout.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(bytes);
            // 480p is sufficient for the screen's default 640x360 render target and limits download costs.
            foreach (var quality in new[] { "hls_480", "hls_720", "hls_1080" })
            {
                if (document.RootElement.TryGetProperty(quality, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) &&
                    IsAniLibertyMedia(uri, ".m3u8"))
                {
                    stream = uri.AbsoluteUri;
                    break;
                }
            }
        }
        catch (Exception e)
        {
            if (!shutdown.IsCancellationRequested)
                Log.Warning($"Cinema AniLiberty resolution failed: {e.Message}");
        }

        _taskManager.RunOnMainThread(() =>
        {
            if (shutdown.IsCancellationRequested || EntityManager.ShuttingDown ||
                !_episodeRequests.TryGetValue(uid, out var currentRequest) || currentRequest != request)
                return;

            _episodeRequests.Remove(uid);
            if (!TryComp<CinemaScreenComponent>(uid, out var current) || current != original ||
                current.Broken || current.VideoUrl != url)
                return;

            if (stream == null || !IsUrlAllowed(stream))
            {
                current.PlayWhenPrepared = false;
                current.AudioStatus = "cinema-status-resolve-error";
                UpdateScreen(uid, current);
                return;
            }

            current.ResolvedVideoUrl = stream;
            // Do not count the API lookup as watched time. Preserve pause/stop/seek controls during lookup.
            if (current.Playing)
                current.ServerStartTime = _timing.RealTime;
            UpdateScreen(uid, current);
            PrepareAudio(uid, current);
        });
    }

    private async Task<byte[]> GetAniLibertyBytesAsync(Uri uri, long limit, CancellationToken token)
    {
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            if (!IsHostAllowed(uri))
                throw new InvalidOperationException("AniLiberty host is not allowed");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Corvax-Cinema/1.0");
            using var response = await _audioHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            if ((int) response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                var target = new Uri(uri, location);
                if (!(IsAniLibertyMedia(target, ".ts") || IsAniLibertyMedia(target, ".m3u8")))
                    throw new InvalidOperationException("Unexpected AniLiberty redirect");
                uri = target;
                continue;
            }
            response.EnsureSuccessStatusCode();
            await response.Content.LoadIntoBufferAsync(limit, token).ConfigureAwait(false);
            return await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Too many AniLiberty redirects");
    }

    private async Task<AudioManifest> StreamAniLibertyAsync(
        string key, Uri playlistUri, int seconds, string cacheRoot, Action<string, int> report,
        Action<StreamingProgress> publish, CancellationToken shutdown)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_cfg.GetCVar(CinemaCVars.AudioExtractionTimeoutSeconds), 30, 7200)));
        var token = timeout.Token;
        var destination = Path.Combine(cacheRoot, key);
        var durationFile = Path.Combine(destination, "duration");
        if (File.Exists(Path.Combine(destination, "complete")) && CountSegments(destination) > 0 && File.Exists(durationFile))
        {
            var cachedDuration = double.Parse(await File.ReadAllTextAsync(durationFile, token).ConfigureAwait(false), CultureInfo.InvariantCulture);
            return new AudioManifest(key, CountSegments(destination), seconds, true, cachedDuration);
        }
        report("cinema-stage-downloading", 0);
        var bytes = await GetAniLibertyBytesAsync(playlistUri, 1024 * 1024, token).ConfigureAwait(false);
        var playlist = System.Text.Encoding.UTF8.GetString(bytes);
        if (!playlist.StartsWith("#EXTM3U", StringComparison.Ordinal) ||
            !playlist.Contains("#EXT-X-ENDLIST", StringComparison.Ordinal) ||
            playlist.Contains("#EXT-X-KEY:", StringComparison.Ordinal) ||
            playlist.Contains("#EXT-X-MAP:", StringComparison.Ordinal) ||
            playlist.Contains("#EXT-X-BYTERANGE:", StringComparison.Ordinal) ||
            playlist.Contains("#EXT-X-GAP", StringComparison.Ordinal) ||
            playlist.Contains("#EXT-X-DISCONTINUITY", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsupported AniLiberty HLS playlist");

        var lines = playlist.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var segments = lines.Where(line => !line.StartsWith('#')).Select(line => new Uri(playlistUri, line)).ToArray();
        double duration = 0;
        var durations = 0;
        foreach (var line in lines.Where(line => line.StartsWith("#EXTINF:", StringComparison.Ordinal)))
        {
            if (!double.TryParse(line["#EXTINF:".Length..].Split(',')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var length) ||
                !double.IsFinite(length) || length <= 0)
                throw new InvalidOperationException("Invalid HLS segment duration");
            duration += length;
            durations++;
        }
        if (segments.Length == 0 || durations != segments.Length || !double.IsFinite(duration) ||
            segments.Any(segment => !IsAniLibertyMedia(segment, ".ts") || !IsHostAllowed(segment)))
            throw new InvalidOperationException("AniLiberty playlist contains invalid media segments");

        var work = Path.Combine(cacheRoot, $".{key}.{Guid.NewGuid():N}");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(durationFile, duration.ToString("R", CultureInfo.InvariantCulture), token).ConfigureAwait(false);
        var ready = 0;
        var maxBytes = Math.Max(1, _cfg.GetCVar(CinemaCVars.AudioMaxInputMiB)) * 1024L * 1024L;
        try
        {
            var count = await CinemaStreamingEncoder.RunAsync(
                _cfg.GetCVar(CinemaCVars.AudioFfmpegPath), work, destination, seconds,
                async (input, cancellation) =>
                {
                    long total = 0;
                    for (var i = 0; i < segments.Length; i++)
                    {
                        var data = await GetAniLibertyBytesAsync(segments[i], Math.Min(32 * 1024 * 1024, maxBytes - total), cancellation)
                            .ConfigureAwait(false);
                        total += data.Length;
                        if (total >= maxBytes)
                            throw new InvalidOperationException("AniLiberty video exceeded the configured download limit");
                        await input.WriteAsync(data, cancellation).ConfigureAwait(false);
                        if (Volatile.Read(ref ready) == 0)
                            report("cinema-stage-downloading", (i + 1) * 100 / segments.Length);
                    }
                },
                count =>
                {
                    Volatile.Write(ref ready, count);
                    publish(new StreamingProgress(count, seconds, duration));
                    report("cinema-stage-streaming", (int) Math.Clamp(count * (double) seconds / duration * 100, 0, 99));
                }, token).ConfigureAwait(false);
            report("cinema-stage-streaming", 100);
            return new AudioManifest(key, count, seconds, true, duration);
        }
        finally
        {
            if (Directory.Exists(work))
                Directory.Delete(work, recursive: true);
        }
    }
}

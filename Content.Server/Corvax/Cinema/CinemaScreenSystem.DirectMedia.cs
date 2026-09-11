using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.Cinema;

namespace Content.Server.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    private async Task<AudioManifest> StreamDirectMediaAsync(string key, string url, int seconds,
        string cacheRoot, Action<string, int> report, Action<StreamingProgress> publish, CancellationToken shutdown)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_cfg.GetCVar(CinemaCVars.AudioExtractionTimeoutSeconds), 30, 7200)));
        var token = timeout.Token;
        var destination = Path.Combine(cacheRoot, key);
        var durationFile = Path.Combine(destination, "duration");
        if (File.Exists(Path.Combine(destination, "complete")) && File.Exists(durationFile))
            return new AudioManifest(key, CountSegments(destination), seconds, true,
                double.Parse(await File.ReadAllTextAsync(durationFile, token).ConfigureAwait(false), CultureInfo.InvariantCulture));

        var work = Path.Combine(cacheRoot, $".{key}.{Guid.NewGuid():N}");
        var maximum = Math.Max(1, _cfg.GetCVar(CinemaCVars.AudioMaxInputMiB)) * 1024L * 1024L;
        await using var proxy = new CinemaMediaProxy(async (request, cancellation) =>
        {
            var uri = new Uri(url);
            for (var redirects = 0; redirects <= 5; redirects++)
            {
                if (!IsUrlAllowed(uri.AbsoluteUri) || !IsDirectMediaUrl(uri.AbsoluteUri))
                    throw new InvalidOperationException($"Media redirect is not an allowed direct MP4/WebM URL: {uri}");
                using var next = new HttpRequestMessage(request.Method, uri);
                next.Headers.UserAgent.ParseAdd("Corvax-Cinema/1.0");
                next.Headers.Range = request.Headers.Range;
                var response = await _audioHttp.SendAsync(next, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
                if ((int) response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    uri = new Uri(uri, location);
                    response.Dispose();
                    continue;
                }
                return response;
            }
            throw new InvalidOperationException("Too many redirects while streaming cinema media");
        }, maximum, token);
        double duration = 0;
        report("cinema-stage-streaming", 0);
        try
        {
            var count = await CinemaStreamingEncoder.RunAsync(_cfg.GetCVar(CinemaCVars.AudioFfmpegPath),
                work, destination, seconds, (_, _) => Task.CompletedTask,
                count =>
                {
                    var total = Volatile.Read(ref duration);
                    publish(new StreamingProgress(count, seconds, total));
                    report("cinema-stage-streaming", total > 0 ? (int) Math.Clamp(count * (double) seconds / total * 100, 0, 99) : -1);
                }, token, proxy.Url, value => Volatile.Write(ref duration, value)).ConfigureAwait(false);
            if (proxy.Error is { } error)
                throw new InvalidOperationException("Cinema media transport failed", error);
            await File.WriteAllTextAsync(durationFile, duration.ToString("R", CultureInfo.InvariantCulture), token).ConfigureAwait(false);
            report("cinema-stage-streaming", 100);
            return new AudioManifest(key, count, seconds, true, duration);
        }
        catch
        {
            File.Delete(Path.Combine(destination, "complete"));
            throw;
        }
        finally
        {
            if (Directory.Exists(work))
                Directory.Delete(work, true);
        }
    }
}

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.Cinema;

namespace Content.Server.Corvax.Cinema;

/// <summary>One continuous MPEG-TS decoder, producing immutable paired WebM/OGG segments before input EOF.</summary>
internal static class CinemaStreamingEncoder
{
    internal static async Task<int> RunAsync(
        string ffmpeg, string work, string destination, int seconds,
        Func<Stream, CancellationToken, Task> feed, Action<int> publish, CancellationToken cancellation,
        string? mediaUrl = null, Action<double>? durationKnown = null)
    {
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(destination);
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var token = stopped.Token;
        var info = Info(ffmpeg);
        info.RedirectStandardInput = true;
        if (mediaUrl == null)
        {
            info.ArgumentList.Add("-f");
            info.ArgumentList.Add("mpegts");
        }
        else
        {
            // Only the local validated HTTP transport is reachable from the decoder.
            info.ArgumentList.Add("-protocol_whitelist");
            info.ArgumentList.Add("http,tcp");
            info.ArgumentList.Add("-format_whitelist");
            info.ArgumentList.Add("mov,matroska,webm");
        }
        foreach (var argument in new[]
                 {
                     "-i", mediaUrl ?? "pipe:0",
                     "-map", "0:v:0", "-an", "-vf", "scale=640:360:force_original_aspect_ratio=decrease,fps=24",
                     "-c:v", "libvpx", "-deadline", "realtime", "-cpu-used", "8", "-threads", "2",
                     "-b:v", "350k", "-maxrate", "500k", "-bufsize", "1000k",
                     "-force_key_frames", $"expr:gte(t,n_forced*{seconds})",
                     "-f", "segment", "-segment_time", seconds.ToString(CultureInfo.InvariantCulture),
                     "-reset_timestamps", "1", Path.Combine(work, "video-%06d.webm"),
                     "-map", "0:a:0", "-vn", "-c:a", "libvorbis", "-q:a", "3", "-ar", "48000", "-ac", "1",
                     "-f", "segment", "-segment_time", seconds.ToString(CultureInfo.InvariantCulture),
                     "-reset_timestamps", "1", Path.Combine(work, "raw-%06d.ogg"),
                 })
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start cinema streaming encoder");
        using var registration = token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        });
        async Task<string> ReadErrors()
        {
            var tail = "";
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                tail = line;
                var marker = line.IndexOf("Duration: ", StringComparison.Ordinal);
                if (marker >= 0 && TimeSpan.TryParse(line[(marker + 10)..].Split(',')[0],
                        CultureInfo.InvariantCulture, out var duration) && duration.TotalSeconds > 0)
                    durationKnown?.Invoke(duration.TotalSeconds);
            }
            return tail;
        }
        var stderr = ReadErrors();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var published = 0;

        async Task Feed()
        {
            try
            {
                await feed(process.StandardInput.BaseStream, token).ConfigureAwait(false);
            }
            catch
            {
                stopped.Cancel();
                throw;
            }
            finally
            {
                process.StandardInput.Close();
            }
        }

        async Task Publish(bool complete)
        {
            while (File.Exists(Path.Combine(work, $"video-{published:D6}.webm")) &&
                   File.Exists(Path.Combine(work, $"raw-{published:D6}.ogg")))
            {
                // Opening the next output closes the preceding one. Never expose a still-open muxer output.
                if (!complete &&
                    (!File.Exists(Path.Combine(work, $"video-{published + 1:D6}.webm")) ||
                     !File.Exists(Path.Combine(work, $"raw-{published + 1:D6}.ogg"))))
                    break;
                var raw = Path.Combine(work, $"raw-{published:D6}.ogg");
                var audio = Path.Combine(work, $"segment-{published:D6}.ogg");
                var video = Path.Combine(work, $"video-{published:D6}.webm");
                var remux = Info(ffmpeg);
                foreach (var argument in new[] { "-i", raw, "-map", "0:a:0", "-c:a", "copy", audio })
                    remux.ArgumentList.Add(argument);
                // Robust's Vorbis decoder requires remuxing the segment muxer's OGG page/granule indices.
                await CinemaScreenSystem.RunPreparationEncoderAsync(remux, "", (_, _) => { }, token).ConfigureAwait(false);
                CheckSize(audio, CinemaAudioChunkEvent.MaxDataBytes);
                CheckSize(video, CinemaVideoChunkEvent.MaxDataBytes);
                MoveIfMissing(audio, Path.Combine(destination, $"segment-{published:D6}.ogg"));
                MoveIfMissing(video, Path.Combine(destination, $"video-{published:D6}.webm"));
                File.Delete(raw);
                publish(++published); // Both files exist and are closed before this count can reach a client.
            }
        }

        var feeding = Feed();
        try
        {
            while (!process.HasExited)
            {
                await Publish(false).ConfigureAwait(false);
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Cinema streaming encoder exited with code {process.ExitCode}: {await stderr.ConfigureAwait(false)}");
            await feeding.ConfigureAwait(false);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            var error = await stderr.ConfigureAwait(false);
            await stdout.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Cinema streaming encoder exited with code {process.ExitCode}: {error}");
            await Publish(true).ConfigureAwait(false);
            if (published == 0 || Directory.GetFiles(work, "video-*.webm").Length != 0 ||
                Directory.GetFiles(work, "raw-*.ogg").Length > 1)
                throw new InvalidOperationException("Streaming encoder did not produce matching audio/video segments");
            // Vorbis may emit one tiny trailing packet beyond the last video frame; it is not a playable pair.
            await File.WriteAllTextAsync(Path.Combine(destination, "complete"), published.ToString(CultureInfo.InvariantCulture), token)
                .ConfigureAwait(false);
            return published;
        }
        finally
        {
            stopped.Cancel();
            try
            {
                await Task.WhenAll(feeding, stderr, stdout).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Preserve the original failure while observing all pipe tasks before deleting working files.
            }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static ProcessStartInfo Info(string ffmpeg)
    {
        var info = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-hide_banner", "-nostdin", "-nostats", "-y" })
            info.ArgumentList.Add(argument);
        return info;
    }

    private static void CheckSize(string file, int maximum)
    {
        var size = new FileInfo(file).Length;
        if (size <= 0 || size > maximum)
            throw new InvalidOperationException($"Cinema segment exceeds its network size limit: {file}");
    }

    private static void MoveIfMissing(string source, string destination)
    {
        // A retry can reuse immutable pairs published by the previous interrupted attempt.
        if (File.Exists(destination))
            File.Delete(source);
        else
            File.Move(source, destination);
    }
}

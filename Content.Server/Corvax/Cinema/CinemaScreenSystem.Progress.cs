using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.Cinema;

namespace Content.Server.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    private sealed record PreparationProgress(string Stage, int Percent);
    private ConcurrentDictionary<string, PreparationProgress> _preparationProgress = new();
    private float _progressAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var screens = EntityQueryEnumerator<CinemaScreenComponent>();
        while (screens.MoveNext(out var uid, out var screen))
        {
            if (screen.StreamPreparing && !screen.Broken)
                UpdateStreamingPlayback(uid, screen);
        }
        _progressAccumulator += frameTime;
        if (_progressAccumulator < 0.25f)
            return;
        _progressAccumulator = 0;

        foreach (var (uid, key) in _entityAudioRequests)
        {
            if (!TryComp<CinemaScreenComponent>(uid, out var comp) || comp.Broken)
                continue;
            if (_streamingProgress.TryGetValue(key, out var stream) && stream.Count > comp.AudioSegmentCount)
                ApplyStreamingProgress(uid, comp, key, stream);
            if (!_preparationProgress.TryGetValue(key, out var progress) ||
                comp.PreparationStage == progress.Stage && comp.PreparationPercent == progress.Percent)
                continue;
            comp.PreparationStage = progress.Stage;
            comp.PreparationPercent = progress.Percent;
            UpdateScreenUi(uid, comp);
        }
    }

    internal static async Task RunPreparationEncoderAsync(
        ProcessStartInfo info, string stage, Action<string, int> report, CancellationToken token)
    {
        info.ArgumentList.Add("-progress");
        info.ArgumentList.Add("pipe:1");
        info.ArgumentList.Add("-nostats");
        report(stage, -1);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start cinema encoder");
        double duration = 0;
        var lastError = "unknown error";

        async Task ReadErrors()
        {
            while (await process.StandardError.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lastError = line;
                var marker = line.IndexOf("Duration: ", StringComparison.Ordinal);
                if (marker < 0)
                    continue;
                var value = line[(marker + "Duration: ".Length)..].Split(',')[0];
                if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed.TotalSeconds > 0)
                    Volatile.Write(ref duration, parsed.TotalSeconds);
            }
        }

        async Task ReadProgress()
        {
            var previous = -1;
            while (await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            {
                var total = Volatile.Read(ref duration);
                if (!line.StartsWith("out_time_us=", StringComparison.Ordinal) || total <= 0 ||
                    !long.TryParse(line["out_time_us=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var micros))
                    continue;
                // Only successful process exit reaches 100%; timestamps can exceed the duration slightly.
                var percent = (int) Math.Clamp(micros / 1000000d / total * 100, 0, 99);
                if (percent <= previous)
                    continue;
                previous = percent;
                report(stage, percent);
            }
        }

        var stderr = ReadErrors();
        var stdout = ReadProgress();
        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            await Task.WhenAll(stderr, stdout).ConfigureAwait(false);
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            try
            {
                await Task.WhenAll(stderr, stdout).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Preserve the original cancellation/process failure after observing both pipe readers.
            }
            throw;
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Cinema encoder exited with code {process.ExitCode}: {lastError}");
        report(stage, 100);
    }
}

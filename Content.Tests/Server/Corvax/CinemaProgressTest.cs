using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Corvax.Cinema;
using NUnit.Framework;

namespace Content.Tests.Server.Corvax;

[TestFixture]
public sealed class CinemaProgressTest
{
    [Test]
    public async Task EncoderReportsActualProgressAndFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cinema-progress-{Guid.NewGuid():N}.wav");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var generate = Info("-f", "lavfi", "-i", "sine=frequency=440", "-t", "3", path);
            Process? process;
            try
            {
                process = Process.Start(generate);
            }
            catch (Win32Exception)
            {
                Assert.Ignore("ffmpeg is required for the encoder progress test");
                return;
            }
            using (process)
            {
                Assert.That(process, Is.Not.Null);
                var error = process!.StandardError.ReadToEndAsync(timeout.Token);
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                Assert.That(process.ExitCode, Is.Zero, await error);
                await output;
            }

            var progress = new ConcurrentQueue<int>();
            await CinemaScreenSystem.RunPreparationEncoderAsync(
                Info("-re", "-i", path, "-f", "null", "-"), "test-stage",
                (stage, percent) => progress.Enqueue(percent), timeout.Token);
            var values = progress.ToArray();
            Assert.That(values.Any(value => value is > 0 and < 100), Is.True, "Must report intermediate progress, not just completion.");
            Assert.That(values, Is.Ordered);
            Assert.That(values.Last(), Is.EqualTo(100));

            progress.Clear();
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await CinemaScreenSystem.RunPreparationEncoderAsync(
                    Info("-i", path + ".missing", "-f", "null", "-"), "test-stage",
                    (_, percent) => progress.Enqueue(percent), timeout.Token));
            Assert.That(progress, Does.Not.Contain(100), "An encoder failure must never report success.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ProcessStartInfo Info(params string[] arguments)
    {
        var info = new ProcessStartInfo("ffmpeg")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("-hide_banner");
        info.ArgumentList.Add("-nostdin");
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return info;
    }
}

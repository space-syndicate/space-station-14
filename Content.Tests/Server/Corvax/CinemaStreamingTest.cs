#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Corvax.Cinema;
using NUnit.Framework;

namespace Content.Tests.Server.Corvax;

[TestFixture]
public sealed class CinemaStreamingTest
{
    [Test]
    public async Task PublishesPlayablePairsBeforeInputEnds()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cinema-stream-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? encoding = null;
        try
        {
            var input = Path.Combine(root, "input.ts");
            await Run(timeout.Token, "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=24",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-t", "22",
                "-c:v", "mpeg2video", "-c:a", "mp2", "-f", "mpegts", input);
            var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var destination = Path.Combine(root, "ready");
            encoding = CinemaStreamingEncoder.RunAsync("ffmpeg", Path.Combine(root, "work"), destination, 5,
                async (pipe, token) =>
                {
                    await using var source = File.OpenRead(input);
                    await source.CopyToAsync(pipe, token);
                    await release.Task.WaitAsync(token); // Keep the input open after writing: playback must not require EOF.
                }, _ => first.TrySetResult(), timeout.Token);
            await first.Task.WaitAsync(timeout.Token);
            Assert.That(encoding.IsCompleted, Is.False);
            Assert.That(File.Exists(Path.Combine(destination, "complete")), Is.False);
            foreach (var file in new[] { "video-000000.webm", "segment-000000.ogg" })
                await Run(timeout.Token, "-v", "error", "-i", Path.Combine(destination, file), "-f", "null", "-");
            release.SetResult();
            Assert.That(await encoding, Is.EqualTo(5));
            Assert.That(File.Exists(Path.Combine(destination, "complete")), Is.True);
            // Use the game's Vorbis decoder too, including later segments with reset timestamps.
            foreach (var file in Directory.GetFiles(destination, "segment-*.ogg"))
            {
                using var source = File.OpenRead(file);
                var loader = typeof(Robust.Shared.GameObjects.EntityUid).Assembly
                    .GetType("Robust.Shared.Audio.AudioLoading.AudioLoaderOgg", throwOnError: true)!;
                var decoded = loader.GetMethod("LoadAudioData")!.Invoke(null, new object[] { source })!;
                var type = decoded.GetType();
                Assert.That((long) type.GetField("TotalSamples")!.GetValue(decoded)!, Is.GreaterThan(0));
                Assert.That((long) type.GetField("Channels")!.GetValue(decoded)!, Is.EqualTo(1));
                var data = (ReadOnlyMemory<short>) type.GetField("Data")!.GetValue(decoded)!;
                Assert.That(data.ToArray(), Has.Some.Not.EqualTo((short) 0), file);
            }

            using var cancel = new CancellationTokenSource();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelledDestination = Path.Combine(root, "cancelled");
            var cancelled = CinemaStreamingEncoder.RunAsync("ffmpeg", Path.Combine(root, "cancel-work"), cancelledDestination, 5,
                async (_, token) => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); }, _ => { }, cancel.Token);
            await started.Task;
            cancel.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await cancelled.WaitAsync(timeout.Token));
            Assert.That(File.Exists(Path.Combine(cancelledDestination, "complete")), Is.False);
        }
        finally
        {
            release.TrySetResult();
            timeout.Cancel();
            if (encoding != null)
            {
                try { await encoding; } catch (Exception) { }
            }
            Directory.Delete(root, true);
        }
    }

    [TestCase("mp4", "mpeg4", "aac")]
    [TestCase("webm", "libvpx", "libvorbis")]
    public async Task DirectMediaSupportsRangesAndPublishesPairs(string extension, string videoCodec, string audioCodec)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cinema-direct-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            var file = Path.Combine(root, "input." + extension);
            // Default MP4 muxing puts moov at the end: decoding requires seeking back to media data.
            await Run(timeout.Token, "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-t", "22",
                "-c:v", videoCodec, "-c:a", audioCodec, file);
            var bytes = await File.ReadAllBytesAsync(file, timeout.Token);
            var rangeRequests = 0;
            await using var proxy = new CinemaMediaProxy((request, _) =>
            {
                var range = request.Headers.Range?.Ranges.Single();
                var offset = range?.From ?? 0;
                var end = range?.To ?? bytes.Length - 1;
                if (offset > 0)
                    Interlocked.Increment(ref rangeRequests);
                var response = new HttpResponseMessage(range == null ? HttpStatusCode.OK : HttpStatusCode.PartialContent);
                response.Content = new ByteArrayContent(bytes, (int) offset, (int) (end - offset + 1));
                response.Headers.AcceptRanges.Add("bytes");
                if (range != null)
                    response.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, end, bytes.Length);
                return Task.FromResult(response);
            }, bytes.Length * 4L, timeout.Token);
            double duration = 0;
            var published = 0;
            var destination = Path.Combine(root, "ready");
            var count = await CinemaStreamingEncoder.RunAsync("ffmpeg", Path.Combine(root, "work"), destination, 5,
                (_, _) => Task.CompletedTask, value =>
                {
                    Assert.That(File.Exists(Path.Combine(destination, $"video-{value - 1:D6}.webm")), Is.True);
                    Assert.That(File.Exists(Path.Combine(destination, $"segment-{value - 1:D6}.ogg")), Is.True);
                    published = value;
                }, timeout.Token, proxy.Url, value => duration = value);
            Assert.That(count, Is.EqualTo(5));
            Assert.That(published, Is.EqualTo(count));
            Assert.That(duration, Is.EqualTo(22).Within(0.2));
            Assert.That(proxy.Error, Is.Null);
            if (extension == "mp4")
                Assert.That(rangeRequests, Is.GreaterThan(0), "MP4 with trailing metadata must support HTTP seeking.");
            foreach (var output in new[] { "video-000000.webm", "segment-000000.ogg" })
                await Run(timeout.Token, "-v", "error", "-i", Path.Combine(destination, output), "-f", "null", "-");
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task Run(CancellationToken token, params string[] arguments)
    {
        var info = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true };
        info.ArgumentList.Add("-nostdin");
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        Process? process;
        try { process = Process.Start(info); }
        catch (Win32Exception) { Assert.Ignore("ffmpeg is required"); return; }
        using (process)
        {
            var error = process!.StandardError.ReadToEndAsync(token);
            var output = process.StandardOutput.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            Assert.That(process.ExitCode, Is.Zero, await error);
            await output;
        }
    }
}

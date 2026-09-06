using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    internal Task<AudioManifest?> GetOrStartAudioJob(
        EntityUid uid, string key, Func<CancellationToken, Task<AudioManifest?>> start)
    {
        _entityAudioRequests[uid] = key;
        if (_audioJobs.TryGetValue(key, out var existing))
            return existing;

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_audioCancellation.Token);
        _audioJobCancellation[key] = cancellation;
        var job = Run();
        _audioJobs[key] = job;
        return job;

        async Task<AudioManifest?> Run()
        {
            using (cancellation)
                return await start(cancellation.Token).ConfigureAwait(false);
        }
    }

    internal void ReleaseAudioRequest(EntityUid uid)
    {
        if (!_entityAudioRequests.Remove(uid, out var key) || _entityAudioRequests.ContainsValue(key))
            return;

        // Retain completed cache entries. An unfinished film only keeps the worker while a screen needs it.
        if (!_audioJobs.TryGetValue(key, out var job) || job.IsCompleted ||
            !_audioJobCancellation.Remove(key, out var cancellation))
            return;

        _audioJobs.TryRemove(key, out _);
        _retiredAudioJobs[job] = key;
        _preparationProgress.TryRemove(key, out _);
        _streamingProgress.TryRemove(key, out _);
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The worker can finish and dispose its source between the completion check and cancellation.
        }
        // Observe cancellation even if the screen disappeared before its normal completion handler ran.
        _ = ObserveRetiredAudioJob(job);
    }

    private static async Task ObserveRetiredAudioJob(Task<AudioManifest?> job)
    {
        try { await job.ConfigureAwait(false); }
        catch (Exception) { }
    }
}

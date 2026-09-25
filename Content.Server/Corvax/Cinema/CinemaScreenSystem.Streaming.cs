using System.Collections.Concurrent;
using Content.Shared.Corvax.Cinema;

namespace Content.Server.Corvax.Cinema;

public sealed partial class CinemaScreenSystem
{
    internal sealed record StreamingProgress(int Count, int SegmentSeconds, double Duration);
    private ConcurrentDictionary<string, StreamingProgress> _streamingProgress = new();

    internal void ApplyStreamingProgress(EntityUid uid, CinemaScreenComponent comp, string key, StreamingProgress progress)
    {
        comp.StreamPreparing = true;
        comp.StreamDuration = progress.Duration;
        comp.AudioCacheKey = key;
        comp.HasVideoSegments = true;
        comp.AudioPlaybackEnabled = _cfg.GetCVar(CinemaCVars.AudioExtractionEnabled);
        comp.AudioSegmentCount = progress.Count;
        comp.AudioSegmentDuration = progress.SegmentSeconds;
        comp.AudioStatus = comp.Buffering ? "cinema-status-buffering" : "cinema-status-paused";
        UpdateStreamingPlayback(uid, comp);
        UpdateScreen(uid, comp);
    }

    internal void UpdateStreamingPlayback(EntityUid uid, CinemaScreenComponent comp)
    {
        if (!comp.StreamPreparing || comp.Broken)
            return;
        var end = PreparedStreamEnd(comp);
        var changed = false;
        if (comp.Playing && CurrentPosition(comp) >= Math.Max(0, end - 0.5))
        {
            comp.PausePosition = Math.Max(0, end - 0.25);
            comp.Playing = false;
            comp.PlayWhenPrepared = true;
            changed = true;
        }
        if (comp.PlayWhenPrepared)
        {
            var buffer = Math.Clamp(_cfg.GetCVar(CinemaCVars.StreamingBufferSeconds), 5, 120);
            var required = comp.StreamDuration > 0
                ? Math.Min(buffer, Math.Max(0, comp.StreamDuration - comp.PausePosition))
                : buffer;
            if (end > comp.PausePosition && end - comp.PausePosition >= required)
            {
                comp.PlayWhenPrepared = false;
                comp.Buffering = false;
                comp.Playing = true;
                comp.ServerStartTime = _timing.RealTime;
                comp.AudioStatus = "cinema-status-paused";
                changed = true;
            }
            else if (!comp.Buffering)
            {
                comp.Buffering = true;
                comp.AudioStatus = "cinema-status-buffering";
                changed = true;
            }
        }
        else if (comp.Buffering && end > comp.PausePosition)
        {
            comp.Buffering = false;
            comp.AudioStatus = "cinema-status-paused";
            changed = true;
        }
        if (changed)
            UpdateScreen(uid, comp);
    }

    private static double PreparedStreamEnd(CinemaScreenComponent comp)
    {
        var end = comp.AudioSegmentCount * (double) comp.AudioSegmentDuration;
        return comp.StreamDuration > 0 ? Math.Min(end, comp.StreamDuration) : end;
    }
}

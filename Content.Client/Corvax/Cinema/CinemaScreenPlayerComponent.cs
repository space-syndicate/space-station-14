using Robust.Client.Graphics;
using Robust.Client.Audio;
using Robust.Client.WebView;
using System.Threading;

namespace Content.Client.Corvax.Cinema;

/// <summary>
/// Client-side runtime state for a single cinema screen entity.
/// Owns the WebView, the render target and the sync bookkeeping. Never networked.
/// </summary>
[RegisterComponent]
public sealed partial class CinemaScreenPlayerComponent : Component
{
    /// <summary>The off-screen WebView that loads the local player.html and plays the remote video.</summary>
    public WebViewControl? WebView;

    /// <summary>Render target the WebView is composited into; its <c>Texture</c> feeds the sprite layer.</summary>
    public IRenderTexture? RenderTexture;

    /// <summary>Accumulates frame time until the next periodic JavaScript sync.</summary>
    public float SyncAccumulator;

    /// <summary>Diagnostic: whether the first JS sync has been logged.</summary>
    public bool FirstSyncLogged;

    /// <summary>Diagnostic: last URL passed to JS (to log meaningful state changes).</summary>
    public string? LastSyncedUrl;

    /// <summary>Diagnostic: last playing flag passed to JS.</summary>
    public bool LastSyncedPlaying;

    /// <summary>Client-side positional audio entity for the currently active generated segment.</summary>
    public EntityUid? AudioEntity;

    /// <summary>Decoded OpenAL stream owned by <see cref="AudioEntity"/>.</summary>
    public AudioStream? AudioStream;

    /// <summary>Generated audio cache key currently associated with this runtime player.</summary>
    public string? AudioCacheKey;

    /// <summary>Segment currently playing, or -1 while none is loaded.</summary>
    public int AudioSegment = -1;

    /// <summary>Segment currently being downloaded, or -1 while idle.</summary>
    public int LoadingAudioSegment = -1;

    /// <summary>Cancels an obsolete segment request after URL/PVS changes.</summary>
    public CancellationTokenSource? AudioCancellation;

    /// <summary>Do not hammer the status endpoint when extraction/download failed.</summary>
    public TimeSpan AudioRetryAt;

}

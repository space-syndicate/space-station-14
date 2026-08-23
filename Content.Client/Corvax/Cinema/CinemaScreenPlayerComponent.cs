using Robust.Client.Graphics;
using Robust.Client.WebView;

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

    /// <summary>Native video resolution reported by player.html via the resource-request bridge.</summary>
    public (int Width, int Height)? SourceVideoSize;
}

using Robust.Shared.GameStates;

namespace Content.Shared.Corvax.Cinema;

/// <summary>
/// A cinema screen entity that renders a remote HTTPS video through a client-side WebView
/// composited into a normal <see cref="SpriteComponent"/> layer.
///
/// Playback state is authoritative on the server. Clients derive the current playback position
/// from <see cref="ServerStartTime"/> and <see cref="PausePosition"/> using server time, so every
/// client shows and hears the same moment without running independent client-side timers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CinemaScreenComponent : Component
{
    /// <summary>
    /// HTTPS URL of the WebM video. It is only ever assigned to <c>video.src</c> inside player.html;
    /// the WebView itself always navigates to a local player.html resource.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? VideoUrl;

    /// <summary>Whether playback is currently active.</summary>
    [DataField, AutoNetworkedField]
    public bool Playing;

    /// <summary>
    /// Server <see cref="Robust.Shared.Timing.IGameTiming.RealTime"/> at the moment the current play
    /// session started. Combined with <see cref="PausePosition"/> this is the single source of truth
    /// for the current position: <c>PausePosition + (serverTime - ServerStartTime)</c> while playing.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan ServerStartTime;

    /// <summary>Video position in seconds to resume from when (re)starting playback.</summary>
    [DataField, AutoNetworkedField]
    public double PausePosition;

    /// <summary>Playback volume in the range [0, 1].</summary>
    [DataField, AutoNetworkedField]
    public float Volume = 1f;

    /// <summary>
    /// True once the screen has been broken. Clients stop playback, release the WebView/render target,
    /// and show the broken sprite.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>WebView/render-target resolution (width, physical pixels).</summary>
    [DataField]
    public int Width = 640;

    /// <summary>WebView/render-target resolution (height, physical pixels).</summary>
    [DataField]
    public int Height = 360;

    /// <summary>Total damage at which the screen enters the broken state.</summary>
    [DataField]
    public float BreakDamage = 100f;

    /// <summary>
    /// Drift threshold in seconds. The JS player resyncs <c>video.currentTime</c> whenever it drifts
    /// further than this from the server-derived position (roughly the requested 0.3–0.5s).
    /// </summary>
    [DataField]
    public float ResyncThreshold = 0.35f;
}

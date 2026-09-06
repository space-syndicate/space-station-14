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

    /// <summary>Films selectable by players. Keys are localized titles; values are direct media URLs.</summary>
    [DataField]
    public Dictionary<string, string> Films = new();

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

    /// <summary>Localized preparation status displayed by the control panel.</summary>
    public string AudioStatus = "cinema-status-empty";

    /// <summary>Cache key of the server-generated segmented OGG audio track.</summary>
    [AutoNetworkedField]
    public string? AudioCacheKey;

    /// <summary>Number of generated OGG segments available through network events.</summary>
    [AutoNetworkedField]
    public int AudioSegmentCount;

    /// <summary>Nominal duration of each generated segment in seconds.</summary>
    [AutoNetworkedField]
    public float AudioSegmentDuration = 30f;

    /// <summary>Distance at which positional cinema audio becomes completely silent.</summary>
    [DataField]
    public float AudioMaxDistance = 20f;

    /// <summary>Distance inside which positional cinema audio plays at full configured volume.</summary>
    [DataField]
    public float AudioFullVolumeDistance = 2f;

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
    /// Drift threshold in seconds. Video and generated positional audio are corrected whenever they drift
    /// further than this from the shared server-derived playback position.
    /// </summary>
    [DataField]
    public float ResyncThreshold = 0.12f;
}

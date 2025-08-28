using Robust.Shared.Configuration;

namespace Content.Shared.Corvax.CCCVars;

/// <summary>
///     Corvax modules console variables
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class CCCVars
{
    /// <summary>
    /// Deny any VPN connections.
    /// </summary>
    public static readonly CVarDef<bool> PanicBunkerDenyVPN =
        CVarDef.Create("game.panic_bunker.deny_vpn", false, CVar.SERVERONLY);

    /**
     * TTS (Text-To-Speech)
     */

    /// <summary>
    /// URL of the TTS server API.
    /// </summary>
    public static readonly CVarDef<bool> TTSEnabled =
        CVarDef.Create("tts.enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// URL of the TTS server API.
    /// </summary>
    public static readonly CVarDef<string> TTSApiUrl =
        CVarDef.Create("tts.api_url", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Auth token of the TTS server API.
    /// </summary>
    public static readonly CVarDef<string> TTSApiToken =
        CVarDef.Create("tts.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Amount of seconds before timeout for API
    /// </summary>
    public static readonly CVarDef<int> TTSApiTimeout =
        CVarDef.Create("tts.api_timeout", 5, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound
    /// </summary>
    public static readonly CVarDef<float> TTSVolume =
        CVarDef.Create("tts.volume", 0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Count of in-memory cached tts voice lines.
    /// </summary>
    public static readonly CVarDef<int> TTSMaxCache =
        CVarDef.Create("tts.max_cache", 250, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Tts rate limit values are accounted in periods of this size (seconds).
    /// After the period has passed, the count resets.
    /// </summary>
    public static readonly CVarDef<float> TTSRateLimitPeriod =
        CVarDef.Create("tts.rate_limit_period", 2f, CVar.SERVERONLY);

    /// <summary>
    /// How many tts preview messages are allowed in a single rate limit period.
    /// </summary>
    public static readonly CVarDef<int> TTSRateLimitCount =
        CVarDef.Create("tts.rate_limit_count", 3, CVar.SERVERONLY);

    /*
     * Peaceful Round End
     */

    /// <summary>
    /// Making everyone a pacifist at the end of a round.
    /// </summary>
    public static readonly CVarDef<bool> PeacefulRoundEnd =
        CVarDef.Create("game.peaceful_end", false, CVar.SERVERONLY);

    /// <summary>
    /// Send station goal on round start or not.
    /// </summary>
    public static readonly CVarDef<bool> StationGoal =
        CVarDef.Create("game.station_goal", true, CVar.SERVERONLY);

    /*
     * Lobby Round Discussion
     */

    /// <summary>
    /// Controls whether the round discussion link is shown in the lobby.
    /// </summary>
    public static readonly CVarDef<bool> LobbyDiscussEnabled =
        CVarDef.Create("lobby.discuss.enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// URL that the round discussion button opens.
    /// </summary>
    public static readonly CVarDef<string> LobbyDiscussLink =
        CVarDef.Create("lobby.discuss.link", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /*
     * Lobby Donation Window
     */

    /// <summary>
    /// Controls whether the donation window is shown in the lobby.
    /// </summary>
    public static readonly CVarDef<bool> LobbyDonateWindowEnabled =
        CVarDef.Create("lobby.donate.window_enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// URL for the project site shown in the lobby donation window.
    /// </summary>
    public static readonly CVarDef<string> LobbyDonateSiteLink =
        CVarDef.Create("lobby.donate.site_link", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}

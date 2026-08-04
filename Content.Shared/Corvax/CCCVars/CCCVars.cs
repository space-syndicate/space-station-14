using Content.Shared.Corvax.TTS;
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

    /*
     * AHelp Discord bot API
     */

    /// <summary>
    /// Enables the external Discord AHelp bot HTTP API.
    /// </summary>
    public static readonly CVarDef<bool> AHelpApiEnabled =
        CVarDef.Create("ahelp.api_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// URL of the Discord AHelp bot HTTP event endpoint.
    /// </summary>
    public static readonly CVarDef<string> AHelpApiUrl =
        CVarDef.Create("ahelp.api_url", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Auth token used by this game server to authenticate with the Discord AHelp bot.
    /// </summary>
    public static readonly CVarDef<string> AHelpApiToken =
        CVarDef.Create("ahelp.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Amount of seconds before timeout for AHelp bot API requests.
    /// </summary>
    public static readonly CVarDef<int> AHelpApiTimeout =
        CVarDef.Create("ahelp.api_timeout", 5, CVar.SERVERONLY | CVar.ARCHIVE);

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
    /// Volume of TTS radio messages
    /// </summary>
    public static readonly CVarDef<float> TTSRadioVolume =
        CVarDef.Create("tts.radio_volume", 1.2f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound
    /// </summary>
    public static readonly CVarDef<float> TTSVolume =
        CVarDef.Create("tts.volume", 1.2f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// TTS voice effect preset. 0 = None.
    /// </summary>
    public static readonly CVarDef<int> TTSVoiceEffect =
        CVarDef.Create("tts.voice_effect", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bitmask of enabled radio channels for TTS. <see cref="RadioChannelFlag"/>
    /// </summary>
    public static readonly CVarDef<int> TTSRadioFilter =
        CVarDef.Create("tts.radio_filter", (int)RadioChannelFlag.AllExceptCommon, CVar.CLIENT | CVar.REPLICATED | CVar.ARCHIVE);

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

    /*
     * Station Goal
     */

    /// <summary>
    /// Send station goal on round start or not.
    /// </summary>
    public static readonly CVarDef<bool> StationGoal =
        CVarDef.Create("game.station_goal", true, CVar.SERVERONLY);
}

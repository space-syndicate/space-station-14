using Robust.Shared.Configuration;

namespace Content.Shared.Corvax.Cinema;

[CVarDefs]
public static class CinemaCVars
{
    /// <summary>
    /// Comma-separated list of allowed video hosts for cinema screens.
    /// Entries may be exact hosts ("media.w3.org") or suffixes ("archive.org" also allows "*.archive.org").
    /// HTTPS is required unless AllowHttp is enabled.
    /// </summary>
    public static readonly CVarDef<string> UrlWhitelist =
        CVarDef.Create("cinema.whitelist", "media.w3.org,storage.googleapis.com,archive.org,upload.wikimedia.org,aniliberty.top,libria.fun", CVar.SERVERONLY);

    /// <summary>
    /// Whether the <see cref="UrlWhitelist"/> domain filter is enforced. Disable this (false) to allow
    /// any host — useful for local/dev media servers.
    /// </summary>
    public static readonly CVarDef<bool> WhitelistEnabled =
        CVarDef.Create("cinema.whitelist_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Allow plain http:// URLs in addition to https:// (https is required by default).
    /// Enable for local/dev media servers that don't have TLS.
    /// </summary>
    public static readonly CVarDef<bool> AllowHttp =
        CVarDef.Create("cinema.allow_http", false, CVar.SERVERONLY);

    /// <summary>
    /// Optional client-side override for the fixed physical WebView/render-target resolution, formatted
    /// "WIDTHxHEIGHT" (e.g. "854x480" or "1280x720"). Empty (default) uses the per-entity
    /// <c>Width</c>/<c>Height</c> (640x360). It intentionally does not follow UI scale or source video size.
    /// </summary>
    public static readonly CVarDef<string> RenderResolution =
        CVarDef.Create("cinema.render_resolution", "", CVar.CLIENTONLY);

    /// <summary>Whether direct MP4/WebM URLs use server-prepared video and positional audio segments.</summary>
    public static readonly CVarDef<bool> AudioExtractionEnabled =
        CVarDef.Create("cinema.audio_extraction_enabled", true, CVar.SERVERONLY);

    /// <summary>ffmpeg executable used by the server for cinema audio extraction.</summary>
    public static readonly CVarDef<string> AudioFfmpegPath =
        CVarDef.Create("cinema.audio_ffmpeg_path", "ffmpeg", CVar.SERVERONLY);

    /// <summary>Maximum direct video download size accepted by the extraction worker, in MiB.</summary>
    public static readonly CVarDef<int> AudioMaxInputMiB =
        CVarDef.Create("cinema.audio_max_input_mib", 2048, CVar.SERVERONLY);

    /// <summary>Maximum time allowed for download and ffmpeg extraction.</summary>
    public static readonly CVarDef<int> AudioExtractionTimeoutSeconds =
        CVarDef.Create("cinema.audio_extraction_timeout_seconds", 1800, CVar.SERVERONLY);

    /// <summary>Length of generated OGG/Vorbis chunks. Short chunks keep client decoded-audio memory bounded.</summary>
    public static readonly CVarDef<int> AudioSegmentSeconds =
        CVarDef.Create("cinema.audio_segment_seconds", 30, CVar.SERVERONLY);

    /// <summary>Prepared media buffered before starting or resuming a cinema stream.</summary>
    public static readonly CVarDef<int> StreamingBufferSeconds =
        CVarDef.Create("cinema.streaming_buffer_seconds", 30, CVar.SERVERONLY);

    /// <summary>Maximum number of generated movie audio tracks retained on the server between uses.</summary>
    public static readonly CVarDef<int> AudioMaxCachedTracks =
        CVarDef.Create("cinema.audio_max_cached_tracks", 4, CVar.SERVERONLY);

    /// <summary>Maximum number of compressed cinema segments retained in a client's memory.</summary>
    public static readonly CVarDef<int> AudioClientCacheSegments =
        CVarDef.Create("cinema.audio_client_cache_segments", 8, CVar.CLIENTONLY);

}

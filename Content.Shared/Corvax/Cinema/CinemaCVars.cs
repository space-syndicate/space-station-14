using Robust.Shared.Configuration;

namespace Content.Shared.Corvax.Cinema;

[CVarDefs]
public static class CinemaCVars
{
    /// <summary>
    /// Comma-separated list of allowed video hosts for cinema screens.
    /// Entries may be exact hosts ("media.w3.org") or suffixes ("archive.org" also allows "*.archive.org").
    /// The URL scheme is always forced to https by the server-side validator.
    /// </summary>
    public static readonly CVarDef<string> UrlWhitelist =
        CVarDef.Create("cinema.whitelist", "media.w3.org,storage.googleapis.com,archive.org,youtube.com,youtu.be", CVar.SERVERONLY);

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
    /// Optional client-side override for the WebView/render-target resolution, formatted "WIDTHxHEIGHT"
    /// (e.g. "854x480" or "1280x720"). Empty (default) uses the per-entity <c>Width</c>/<c>Height</c>
    /// (640x360). Set it to the source video resolution to avoid downscaling (better quality, more CPU
    /// for CEF's software rendering).
    /// </summary>
    public static readonly CVarDef<string> RenderResolution =
        CVarDef.Create("cinema.render_resolution", "", CVar.CLIENTONLY);
}

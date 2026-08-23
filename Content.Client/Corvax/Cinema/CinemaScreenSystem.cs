using System.Globalization;
using System.Numerics;
using Content.Shared.Corvax.Cinema;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.WebView;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.Cinema;

/// <summary>
/// Client side of the cinema screen: owns a WebView per screen, composites it into a render target,
/// feeds the resulting texture to a sprite layer, and periodically synchronizes the JS player with
/// the server-authoritative playback state (server time is the only clock).
/// </summary>
public sealed partial class CinemaScreenSystem : EntitySystem
{
    // NOTE: use an explicit host. CEF/GURL mangles `res:///Textures/...` (empty authority) into
    // `res://Textures/...` (host = "textures"), which breaks `new ResPath(uri.AbsolutePath)`.
    private const string PlayerHtmlUrl = "res://localhost/Textures/Corvax/Cinema/player.html";
    private const string VideoLayerKey = "screenVideo";
    private const string FrameLayerKey = "screenFrame";
    private const float SyncInterval = 0.1f;

    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private CinemaScreenOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CinemaScreenOverlay();
        _overlayManager.AddOverlay(_overlay);

        SubscribeLocalEvent<CinemaScreenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CinemaScreenComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CinemaScreenComponent, CinemaScreenPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp, out var player))
        {
            UpdatePlayer(uid, comp, player, frameTime);
        }
    }

    private void UpdatePlayer(EntityUid uid, CinemaScreenComponent comp, CinemaScreenPlayerComponent player, float frameTime)
    {
        var webView = player.WebView;
        if (webView == null)
            return;

        // Re-attach if the WebView was created before the UI root was ready.
        if (webView.Parent == null)
        {
            var host = new LayoutContainer();
            LayoutContainer.SetPosition(webView, new Vector2(-10000, -10000));
            host.AddChild(webView);
            _uiManager.RootControl?.AddChild(host);
        }

        // Auto-match the source video resolution (no downscale) unless a manual override is set.
        if (string.IsNullOrWhiteSpace(_cfg.GetCVar(CinemaCVars.RenderResolution)) &&
            player.SourceVideoSize is { } source &&
            ((int) webView.SetSize.X != source.Width || (int) webView.SetSize.Y != source.Height))
        {
            webView.SetSize = new Vector2(source.Width, source.Height);
        }

        // Ensure the render target matches the WebView's physical size (which may depend on UI scale).
        var size = webView.PixelSize;
        if (size.X > 0 && size.Y > 0 && (player.RenderTexture == null || player.RenderTexture.Size != size))
        {
            player.RenderTexture?.Dispose();
            player.RenderTexture = _clyde.CreateRenderTarget(
                size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                new TextureSampleParameters { Filter = true },
                "cinema-screen");

            ApplyVideoTexture(uid, comp, player);
            player.SyncAccumulator = SyncInterval; // sync immediately on (re)creation.
            Log.Info($"Cinema render target created: {size} (entity {ToPrettyString(uid)})");
        }

        // Broken: stop playback, free the WebView/render target and show the broken sprite.
        if (comp.Broken)
        {
            if (webView != null)
            {
                ReleaseWebView(player);
                ShowBroken(uid);
            }

            return;
        }

        player.SyncAccumulator += frameTime;
        if (player.SyncAccumulator < SyncInterval)
            return;

        player.SyncAccumulator = 0f;
        SyncPlayback(comp, player);
    }

    private void SyncPlayback(CinemaScreenComponent comp, CinemaScreenPlayerComponent player)
    {
        var webView = player.WebView;
        if (webView == null)
            return;

        var url = comp.VideoUrl;
        var playing = comp.Playing;
        var time = CurrentPosition(comp);
        var volume = comp.Volume;

        // player.html compares the values and only acts on meaningful changes (src swap, drift > threshold,
        // play/pause, volume). The URL is only ever assigned to video.src inside player.html.
        // Note: build with invariant-formatted values + concatenation; `string.Create(IFormatProvider, ...)`
        // (i.e. culture-formatted interpolation) is not sandbox-whitelisted.
        var js = "cinema.applyState(" +
                 JsString(url) + ", " +
                 (playing ? "true" : "false") + ", " +
                 time.ToString("R", CultureInfo.InvariantCulture) + ", " +
                 volume.ToString("R", CultureInfo.InvariantCulture) + ", " +
                 comp.ResyncThreshold.ToString("R", CultureInfo.InvariantCulture) +
                 ");";

        if (!player.FirstSyncLogged)
        {
            player.FirstSyncLogged = true;
            Log.Info($"Cinema first sync: url='{url}', playing={playing}, time={time:F2}, volume={volume:F2}");
        }

        if (url != player.LastSyncedUrl || playing != player.LastSyncedPlaying)
        {
            Log.Info($"Cinema sync state change: url='{url}', playing={playing}, time={time:F2}");
            player.LastSyncedUrl = url;
            player.LastSyncedPlaying = playing;
        }

        try
        {
            webView.ExecuteJavaScript(js);
        }
        catch (Exception e)
        {
            Log.Error($"Cinema sync JS failed: {e.Message}");
        }
    }

    /// <summary>The position (seconds) the video should currently be at, derived from server time.</summary>
    private double CurrentPosition(CinemaScreenComponent comp)
    {
        if (!comp.Playing)
            return comp.PausePosition;

        var elapsed = (_timing.ServerTime - comp.ServerStartTime).TotalSeconds;
        return comp.PausePosition + Math.Max(0, elapsed);
    }

    private void OnStartup(EntityUid uid, CinemaScreenComponent comp, ComponentStartup args)
    {
        var player = EnsureComp<CinemaScreenPlayerComponent>(uid);
        CreateWebView(uid, comp, player);
    }

    private void OnShutdown(EntityUid uid, CinemaScreenComponent comp, ComponentShutdown args)
    {
        if (TryComp<CinemaScreenPlayerComponent>(uid, out var player))
            ReleaseWebView(player);
    }

    private void CreateWebView(EntityUid uid, CinemaScreenComponent comp, CinemaScreenPlayerComponent player)
    {
        ReleaseWebView(player);

        var (width, height) = GetRenderResolution(comp);

        var webView = new WebViewControl
        {
            SetSize = new Vector2(width, height),
        };

        // Keep the WebView off-screen: it only exists so it gets laid out (sizing the CEF view + texture).
        // The actual compositing happens in CinemaScreenOverlay, which renders this control into a render target.
        var host = new LayoutContainer();
        LayoutContainer.SetPosition(webView, new Vector2(-10000, -10000));
        host.AddChild(webView);

        webView.Url = PlayerHtmlUrl;

        // JS->C# bridge: player.html reports the native video size through a resource request so the
        // render target can match the source resolution (no downscale). `AddResourceRequestHandler` is
        // part of the public Robust.Client.WebView API.
        webView.AddResourceRequestHandler(ctx => OnBridgeRequest(player, ctx));

        player.WebView = webView;
        player.RenderTexture = null;
        player.SyncAccumulator = 0f;

        _uiManager.RootControl?.AddChild(host);
        Log.Info($"Cinema WebView created for {ToPrettyString(uid)} (set size {width}x{height})");
    }

    private void OnBridgeRequest(CinemaScreenPlayerComponent player, IRequestHandlerContext ctx)
    {
        const string marker = "res://localhost/cinema-video-size";
        if (string.IsNullOrEmpty(ctx.Url) ||
            !ctx.Url.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (TryParseQueryInt(ctx.Url, "w", out var width) &&
            TryParseQueryInt(ctx.Url, "h", out var height) &&
            width > 0 && height > 0)
        {
            player.SourceVideoSize = (width, height);
        }

        // It is a control signal, not a real resource.
        ctx.DoCancel();
    }

    private static bool TryParseQueryInt(string url, string key, out int value)
    {
        value = 0;

        var marker = key + "=";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var start = idx + marker.Length;
        var end = start;
        while (end < url.Length && char.IsDigit(url[end]))
            end++;

        return end > start && int.TryParse(url.Substring(start, end - start), out value);
    }

    private (int Width, int Height) GetRenderResolution(CinemaScreenComponent comp)
    {
        var cvar = _cfg.GetCVar(CinemaCVars.RenderResolution);
        if (!string.IsNullOrWhiteSpace(cvar))
        {
            var parts = cvar.Split('x', 'X');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var width) &&
                int.TryParse(parts[1], out var height) &&
                width > 0 && height > 0)
            {
                return (width, height);
            }
        }

        return (comp.Width, comp.Height);
    }

    private void ReleaseWebView(CinemaScreenPlayerComponent player)
    {
        var webView = player.WebView;
        if (webView != null)
        {
            var host = webView.Parent;
            webView.Orphan(); // ExitedTree closes the browser.
            host?.Orphan();
            player.WebView = null;
        }

        player.RenderTexture?.Dispose();
        player.RenderTexture = null;
    }

    private void ApplyVideoTexture(EntityUid uid, CinemaScreenComponent comp, CinemaScreenPlayerComponent player)
    {
        if (player.RenderTexture == null)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetTexture((uid, sprite), VideoLayerKey, player.RenderTexture.Texture);

        // The frame layer's scale (set in the prototype YAML) defines the screen's world size.
        // Scale the video layer so its (possibly larger) render texture maps to the same world width.
        // Frame texture is 640px wide.
        var frameScale = sprite[FrameLayerKey].Scale.X;
        var videoScale = frameScale * 640f / player.RenderTexture.Size.X;
        _sprite.LayerSetScale((uid, sprite), VideoLayerKey, new Vector2(videoScale, videoScale));
        _sprite.LayerSetVisible((uid, sprite), VideoLayerKey, true);
    }

    private void ShowBroken(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetVisible((uid, sprite), VideoLayerKey, false);

        var broken = _resourceCache.GetResource<TextureResource>(
            new ResPath("/Textures/Corvax/Structures/Furniture/cinema_screen_broken.png"));

        // The broken texture is also 640px, so the frame scale from the prototype stays correct.
        _sprite.LayerSetTexture((uid, sprite), FrameLayerKey, broken.Texture);
    }

    private static string JsString(string? value)
    {
        if (value == null)
            return "null";

        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "")
            .Replace("\n", " ") + "\"";
    }
}

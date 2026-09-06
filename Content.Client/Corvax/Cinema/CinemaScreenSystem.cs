using System.Globalization;
using System.Numerics;
using Content.Shared.Corvax.Cinema;
using Content.Shared.GameTicking;
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
    private const int MinRenderDimension = 64;
    private const int MaxRenderDimension = 4096;

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

        SubscribeNetworkEvent<CinemaAudioChunkEvent>(OnAudioSegment);

        SubscribeLocalEvent<CinemaScreenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CinemaScreenComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Shutdown()
    {
        ClearAudioCache();
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        ClearAudioCache();
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

        UpdateAudio(uid, comp, player);

        // Re-attach if the WebView was created before the UI root was ready.
        if (!webView.IsInsideTree)
        {
            var host = webView.Parent;
            if (host == null)
            {
                host = new LayoutContainer();
                LayoutContainer.SetPosition(webView, new Vector2(-10000, -10000));
                host.AddChild(webView);
            }

            if (host.Parent == null)
                _uiManager.RootControl?.AddChild(host);
        }

        // WebViewControl sizes are expressed in UI pixels, but CEF and the render target use physical pixels.
        // Counter-scale the control so changing display/UI scale never resizes CEF or recreates a huge chain of
        // intermediate render targets. The small physical-pixel bias avoids float truncation to size - 1.
        var (width, height) = GetRenderResolution(comp);
        var desiredSize = new Vector2i(width, height);
        if (webView.PixelSize != desiredSize)
        {
            var uiScale = MathF.Max(webView.UIScale, 0.01f);
            webView.SetSize = new Vector2(width + 0.25f, height + 0.25f) / uiScale;
        }

        // Wait for the UI layout pass to apply the requested physical size before allocating the target. This
        // prevents transient 0px/old-size targets while the window or UI scale is changing.
        if (webView.PixelSize == desiredSize &&
            (player.RenderTexture == null || player.RenderTexture.Size != desiredSize))
        {
            player.RenderTexture?.Dispose();
            player.RenderTexture = _clyde.CreateRenderTarget(
                desiredSize,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                new TextureSampleParameters { Filter = true },
                "cinema-screen");

            ApplyVideoTexture(uid, comp, player);
            player.SyncAccumulator = SyncInterval; // sync immediately on (re)creation.
            Log.Debug($"Cinema render target created: {desiredSize} (entity {ToPrettyString(uid)})");
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
        SyncPlayback(uid, comp, player);
    }

    private void SyncPlayback(EntityUid uid, CinemaScreenComponent comp, CinemaScreenPlayerComponent player)
    {
        var webView = player.WebView;
        if (webView == null)
            return;

        var url = comp.VideoUrl;
        // CEF audio is not a game AudioComponent, so it does not automatically follow entity PVS. Actually pause
        // the browser player outside PVS (instead of merely muting it); on re-entry the server clock seeks it to
        // the current position before playback resumes.
        var inPvs = IsInPvs(uid);
        var playing = comp.Playing && inPvs;
        var time = CurrentPosition(comp);
        // Browser audio is permanently disabled. Direct-media audio is extracted server-side and played through
        // the normal positional game AudioSystem instead, which owns attenuation and PVS behavior.

        // player.html compares the values and only acts on meaningful changes (src swap, drift > threshold,
        // play/pause). The URL is only ever assigned to video.src inside player.html.
        // Note: build with invariant-formatted values + concatenation; `string.Create(IFormatProvider, ...)`
        // (i.e. culture-formatted interpolation) is not sandbox-whitelisted.
        var js = "cinema.applyState(" +
                 JsString(url) + ", " +
                 (playing ? "true" : "false") + ", " +
                 time.ToString("R", CultureInfo.InvariantCulture) + ", " +
                 comp.ResyncThreshold.ToString("R", CultureInfo.InvariantCulture) +
                 ");";

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
            ReleaseWebView(player, EntityManager.ShuttingDown);
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

        player.WebView = webView;
        player.RenderTexture = null;
        player.SyncAccumulator = 0f;

        _uiManager.RootControl?.AddChild(host);
        // The CEF manager closes every active browser before the entity manager flushes entities during client
        // shutdown. Keeping this set prevents the later UI-tree exit from trying to close the same browser again.
        webView.AlwaysActive = true;
        Log.Debug($"Cinema WebView created for {ToPrettyString(uid)} (set size {width}x{height})");
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
                width is >= MinRenderDimension and <= MaxRenderDimension &&
                height is >= MinRenderDimension and <= MaxRenderDimension)
            {
                return (width, height);
            }
        }

        return (Math.Clamp(comp.Width, MinRenderDimension, MaxRenderDimension),
            Math.Clamp(comp.Height, MinRenderDimension, MaxRenderDimension));
    }

    private void ReleaseWebView(CinemaScreenPlayerComponent player, bool clientShuttingDown = false)
    {
        var webView = player.WebView;
        player.WebView = null;

        if (webView != null)
        {
            if (!clientShuttingDown)
            {
                var host = webView.Parent;
                // AlwaysActive=false does not close while the control is in the tree. Orphan then invokes
                // ExitedTree and closes the browser exactly once.
                webView.AlwaysActive = false;
                webView.Orphan();
                host?.Orphan();
            }
            // During full client shutdown WebViewManagerCef has already closed active browsers. Leave the control
            // in the root tree with AlwaysActive=true so the subsequent tree teardown does not call CloseBrowser.
        }

        player.RenderTexture?.Dispose();
        player.RenderTexture = null;
        ReleaseAudio(player);
    }

    private void ApplyVideoTexture(EntityUid uid, CinemaScreenComponent comp, CinemaScreenPlayerComponent player)
    {
        if (player.RenderTexture == null)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetTexture((uid, sprite), VideoLayerKey, player.RenderTexture.Texture);
        sprite.LayerSetShader(VideoLayerKey, "unshaded");

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

    private bool IsInPvs(EntityUid uid)
    {
        return TryComp(uid, out MetaDataComponent? metadata) &&
               (metadata.Flags & MetaDataFlags.Detached) == 0;
    }

}

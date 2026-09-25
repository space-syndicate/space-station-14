using System.Numerics;
using Content.Shared.Corvax.Cinema;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client.Corvax.Cinema;

/// <summary>
/// Renders each cinema screen's WebView into its render target every frame so the resulting texture can be
/// used as a normal <see cref="SpriteComponent"/> layer. This work deliberately runs in screen space: doing
/// nested UI rendering from a world/lighting pass leaks the active camera and lighting render state into the
/// WebView texture. The resulting sprite itself is still rendered normally in the world.
/// </summary>
public sealed partial class CinemaScreenOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    // Render before the game viewport. In windowed mode, running the nested render target after the world can leave
    // the window viewport/scissor active and produce a black strip along the top edge until the next full redraw.
    public override OverlaySpace Space => OverlaySpace.ScreenSpaceBelowWorld;

    public CinemaScreenOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // `args` is a ref struct, so capture the render handle (a reference type) before the lambda.
        var renderHandle = args.RenderHandle;
        var query = _entityManager.EntityQueryEnumerator<CinemaScreenComponent, CinemaScreenPlayerComponent>();

        while (query.MoveNext(out var uid, out _, out var player))
        {
            if (!_entityManager.TryGetComponent(uid, out MetaDataComponent? metadata) ||
                (metadata.Flags & MetaDataFlags.Detached) != 0)
            {
                continue;
            }

            var webView = player.WebView;
            var target = player.RenderTexture;

            if (webView == null || target == null || target.Size.X <= 0 || target.Size.Y <= 0)
                continue;

            renderHandle.RenderInRenderTarget(target, () =>
            {
                _uiManager.RenderControl(renderHandle, webView, Vector2i.Zero);
            }, Color.Black);
        }
    }
}

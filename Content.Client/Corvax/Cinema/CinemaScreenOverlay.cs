using System.Numerics;
using Content.Shared.Corvax.Cinema;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client.Corvax.Cinema;

/// <summary>
/// Renders each cinema screen's WebView into its render target every frame so the resulting texture can be
/// used as a normal <see cref="SpriteComponent"/> layer (i.e. full world rendering: FOV, lighting,
/// occlusion by entities, and movement with the entity).
/// </summary>
public sealed partial class CinemaScreenOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public CinemaScreenOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // `args` is a ref struct, so capture the render handle (a reference type) before the lambda.
        var renderHandle = args.RenderHandle;
        var query = _entityManager.EntityQueryEnumerator<CinemaScreenComponent, CinemaScreenPlayerComponent>();

        while (query.MoveNext(out _, out _, out var player))
        {
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

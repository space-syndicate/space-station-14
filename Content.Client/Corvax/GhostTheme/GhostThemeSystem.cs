using Content.Client.Corvax.Sponsors;
using Content.Client.Ghost;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Corvax.GhostTheme;

public sealed class GhostThemeSystem : EntitySystem
{
    [Dependency] private readonly SponsorsManager _sponsorsManager = default!;
    [Dependency] private readonly GhostSystem _ghostSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        _ghostSystem.PlayerAttached += OnPlayerAttached;
    }

    private void OnPlayerAttached(GhostComponent comp)
    {
        if (
            _sponsorsManager.TryGetInfo(out var sponsor) &&
            sponsor.GhostTheme != null &&
            _prototypeManager.TryIndex(sponsor.GhostTheme, out GhostThemePrototype? ghostTheme) &&
            TryComp<SpriteComponent>(comp.Owner, out var sprite))
        {
            sprite.LayerSetState(0, ghostTheme.Icon);
        }
    }
}

using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Overlays;

namespace Content.Server.Corvax.Administration.Systems;

public sealed class AdminHealthOverlaySystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly Component[] _overlays =
    [
        new ShowHealthBarsComponent(), new ShowHealthIconsComponent(),
        new ShowJobIconsComponent(), new ShowMindShieldIconsComponent(),
        new ShowCriminalRecordIconsComponent()
    ];

    public override void Initialize()
    {
        SubscribeLocalEvent<GhostComponent, ActionToggleHealthOverlayEvent>(OnHealthOverlayToggled);
    }

    private void OnHealthOverlayToggled(Entity<GhostComponent> ent, ref ActionToggleHealthOverlayEvent args)
    {
        // EnsureComponent hasn't templateless signature and template signature can't be called with type reference
        if (Array.TrueForAll(_overlays, overlay => !_entityManager.HasComponent(ent.Owner, overlay.GetType())))
        {
            Array.ForEach(_overlays, overlay => _entityManager.AddComponent(ent.Owner, overlay));
            return;
        }

        Array.ForEach(_overlays,  overlay => _entityManager.RemoveComponent(ent.Owner, overlay));
    }
}

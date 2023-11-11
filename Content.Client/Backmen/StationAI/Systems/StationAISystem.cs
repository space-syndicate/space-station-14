using Content.Shared.Backmen.StationAI.Events;
using Content.Shared.Overlays;

namespace Content.Client.Backmen.StationAI;

public sealed class StationAISystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NetworkedAIHealthOverlayEvent>(OnHealthOverlayEvent);
    }

    private void OnHealthOverlayEvent(NetworkedAIHealthOverlayEvent args)
    {
        var uid = GetEntity(args.Performer);

        if (!_entityManager.TryGetComponent<ShowHealthBarsComponent>(uid, out var health))
        {
            health = _entityManager.AddComponent<ShowHealthBarsComponent>(uid);
        }
        else
        {
            _entityManager.RemoveComponent<ShowHealthBarsComponent>(uid);
        }
    }
}

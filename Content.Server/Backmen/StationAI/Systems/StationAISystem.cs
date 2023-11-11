using Content.Server.Backmen.Abilities.Psionics;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Content.Shared.Backmen.StationAI.Events;

namespace Content.Shared.Backmen.StationAI;

public sealed class StationAISystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StationAIComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StationAIComponent, EntityTerminatingEvent>(OnTerminated);

        SubscribeLocalEvent<AIHealthOverlayEvent>(OnHealthOverlayEvent);
    }

    private void OnTerminated(Entity<StationAIComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TryComp<MindSwappedComponent>(ent, out var mindSwappedComponent))
            return;
        if (mindSwappedComponent.OriginalEntity.Valid)
        {
            QueueDel(mindSwappedComponent.OriginalEntity);
        }
    }

    private void OnStartup(EntityUid uid, StationAIComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionId, component.Action);
    }

    private void OnShutdown(EntityUid uid, StationAIComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionId);
    }

    private void OnHealthOverlayEvent(AIHealthOverlayEvent args)
    {
        RaiseNetworkEvent(new NetworkedAIHealthOverlayEvent(GetNetEntity(args.Performer)));
        args.Handled = true;
    }
}

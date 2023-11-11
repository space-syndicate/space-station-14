using Content.Shared.Throwing;
using Content.Shared.Item;
using Content.Shared.Strip.Components;
using Content.Shared.Hands;

namespace Content.Shared.Backmen.StationAI;

public sealed class StationAISystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, ThrowAttemptEvent>(OnDisallowedEvent);
        SubscribeLocalEvent<StationAIComponent, PickupAttemptEvent>(OnDisallowedEvent);
        SubscribeLocalEvent<StationAIComponent, DropAttemptEvent>(OnDisallowedEvent);
        SubscribeLocalEvent<StationAIComponent, StrippingSlotButtonPressed>(OnStripEvent);
    }

    private void OnDisallowedEvent(EntityUid uid, Component component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnStripEvent(EntityUid uid, Component component, StrippingSlotButtonPressed args)
    {
        return;
    }
}

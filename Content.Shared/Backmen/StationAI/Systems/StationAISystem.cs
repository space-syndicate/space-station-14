using Content.Shared.Throwing;
using Content.Shared.Item;
using Content.Shared.Strip.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Physics.Pull;

namespace Content.Shared.Backmen.StationAI;

public sealed class StationAISystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();


        SubscribeLocalEvent<StationAIComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, IsEquippingAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, IsUnequippingAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StationAIComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<StationAIComponent, ChangeDirectionAttemptEvent>(OnUpdateCanMove);

        SubscribeLocalEvent<StationAIComponent, StrippingSlotButtonPressed>(OnStripEvent);
    }

    private void OnAttempt(EntityUid uid, StationAIComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnUpdateCanMove(EntityUid uid, StationAIComponent component, CancellableEntityEventArgs args)
    {
        if(!HasComp<AIEyeComponent>(uid))
            args.Cancel();
    }

    private void OnStripEvent(EntityUid uid, Component component, StrippingSlotButtonPressed args)
    {
        return;
    }
}

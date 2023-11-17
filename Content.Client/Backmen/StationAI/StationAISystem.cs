using Content.Client.Storage.Components;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;

namespace Content.Client.Backmen.StationAI;

public sealed class StationAISystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationAIComponent, InteractionAttemptEvent>(CanInteraction);
    }

    private void CanInteraction(Entity<StationAIComponent> ent, ref InteractionAttemptEvent args)
    {
        var core = ent;
        if (TryComp<AIEyeComponent>(ent, out var eye))
        {
            if (eye.AiCore == null)
            {
                args.Cancel();
                return;
            }

            core = eye.AiCore.Value;
        }

        if (!core.Owner.Valid)
        {
            args.Cancel();
            return;
        }

        if (args.Target != null && Transform(core).GridUid != Transform(args.Target.Value).GridUid)
        {
            args.Cancel();
            return;
        }


        if (HasComp<ItemComponent>(args.Target))
        {
            args.Cancel();
            return;
        }

        if (HasComp<EntityStorageComponent>(args.Target))
        {
            args.Cancel();
            return;
        }
    }
}

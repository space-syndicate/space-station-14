using Content.Server.Power.Components;
using Content.Server.Storage.Components;
using Content.Shared.Actions;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Backmen.StationAI.Events;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class StationAISystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StationAIComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StationAIComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StationAIComponent, AIDroneChangeEvent>(OnDroneChange);
        SubscribeLocalEvent<StationAIComponent, EntityTerminatingEvent>(OnTerminated);

        SubscribeLocalEvent<StationAIComponent, InteractionAttemptEvent>(CanInteraction);
    }

    private void OnInit(EntityUid uid, StationAIComponent component, ComponentInit args)
    {
        if (!_entityManager.HasComponent<StationAIComponent>(uid))
            return;

        _actions.AddAction(uid,
            ref component.AiDroneChangeAction,
            component.AiDroneChangeActionPrototype);
    }

    private void OnDroneChange(EntityUid uid, StationAIComponent component, AIDroneChangeEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<StationAiDroneComponent>(uid))
        {
            if (
                !TryComp<VisitingMindComponent>(args.Performer, out var mindId) ||
                mindId!.MindId == null ||
                !TryComp<MindComponent>(mindId.MindId.Value, out var mind)
            )
                return;

            _mindSystem.UnVisit(mindId.MindId.Value, mind);
        }
        else
        {
            if (!_mindSystem.TryGetMind(args.Performer, out var mindId, out var mind))
                return;

            if (component.AiDrone == null)
            {
                SpawnDrone(uid, component);
            }

            _mindSystem.UnVisit(mindId, mind);

            if (component.AiDrone != null)
                _mindSystem.Visit(mindId, component.AiDrone.Value, mind);
        }

        args.Handled = true;
    }

    private void SpawnDrone(EntityUid uid, StationAIComponent component)
    {
        var coords = Transform(uid).Coordinates;
        var drone = _entityManager.CreateEntityUninitialized(component.AiDronePrototype, coords);

        component.AiDrone = drone;

        EnsureComp<StationAIComponent>(drone).SelectedLaw = component.SelectedLaw;
        EnsureComp<SiliconLawBoundComponent>(drone);
        EnsureComp<StationAiDroneComponent>(drone);

        _entityManager.InitializeAndStartEntity(drone, coords.GetMapId(_entityManager));

        if (TryComp<BrokenAiComponent>(uid, out _))
        {
            _entityManager.AddComponent<BrokenAiComponent>(drone);
        }

        _transformSystem.AttachToGridOrMap(drone);
    }

    private void CanInteraction(Entity<StationAIComponent> ent, ref InteractionAttemptEvent args)
    {
        var core = ent;
        if (TryComp<AIEyeComponent>(ent, out var eye))
        {
            if (eye.AiCore == null)
            {
                QueueDel(ent);
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

        if (!TryComp<ApcPowerReceiverComponent>(core, out var power))
        {
            args.Cancel();
            return;
        }

        if (power is { NeedsPower: true, Powered: false })
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

        if (TryComp<ApcPowerReceiverComponent>(args.Target, out var targetPower) && targetPower.NeedsPower && !targetPower.Powered)
        {
            args.Cancel();
        }
    }

    private void OnTerminated(Entity<StationAIComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.ActiveEye.IsValid())
        {
            return;
        }
        QueueDel(ent.Comp.ActiveEye);
    }

    private void OnStartup(EntityUid uid, StationAIComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionId, component.Action);
        _hands.AddHand(uid,"SAI",HandLocation.Middle);
    }

    private void OnShutdown(EntityUid uid, StationAIComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionId);
        _actions.RemoveAction(uid, component.AiDroneChangeAction);
    }
}

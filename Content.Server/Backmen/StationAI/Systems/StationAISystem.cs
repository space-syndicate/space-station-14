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
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class StationAISystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StationAIComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StationAIComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StationAIComponent, AIDroneChangeEvent>(OnDroneChange);
        SubscribeLocalEvent<StationAIComponent, EntityTerminatingEvent>(OnTerminated);

        SubscribeLocalEvent<StationAIComponent, InteractionAttemptEvent>(CanInteraction);

        SubscribeLocalEvent<StationAiDroneComponent, MobStateChangedEvent>(OnDroneStateChange);
    }

    private void OnDroneStateChange(EntityUid uid, StationAiDroneComponent component, MobStateChangedEvent args)
    {
        if (!_mobState.IsDead(uid))
            return;

        if (component.AiCore != null && component.AiCore.Value.IsValid())
        {
            if (TryComp<StationAIComponent>(component.AiCore, out var stationAi))
            {
                stationAi.AiDrone = null;
            }

            if (
                !TryComp<VisitingMindComponent>(uid, out var mindId) ||
                mindId!.MindId == null ||
                !TryComp<MindComponent>(mindId.MindId.Value, out var mind)
            )
                return;

            _mindSystem.UnVisit(mindId.MindId.Value, mind);
        }
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
            {
                if (!TryComp<VisitingMindComponent>(args.Performer, out var mindVId) ||
                    mindVId!.MindId == null ||
                    !TryComp<MindComponent>(mindVId.MindId.Value, out mind))
                    return;
                mindId = mindVId.MindId.Value;
            }

            if (component.AiDrone == null || !component.AiDrone.Value.IsValid())
            {
                var elapsed = _timing.CurTime - component.LastDroneSpawn;
                if (component.LastDroneSpawn != TimeSpan.Zero && elapsed < component.DroneSpawnDelay)
                {
                    _actions.SetCooldown(component.AiDroneChangeAction, elapsed);
                    _popupSystem.PopupEntity(Loc.GetString("drone-wait-delay"), uid);

                    return;
                }

                component.LastDroneSpawn = _timing.CurTime;
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
        var coords = Transform(component.Core ?? uid).Coordinates;
        var drone = _entityManager.CreateEntityUninitialized(component.AiDronePrototype, coords);

        EntityUid? aiCore = TryComp<AIEyeComponent>(uid, out var eye) ? eye.AiCore : uid!;
        var metaName = EnsureComp<MetaDataComponent>(uid).EntityName;

        component.AiDrone = drone;

        var stationAi = EnsureComp<StationAIComponent>(drone);
        stationAi.SelectedLaw = component.SelectedLaw;
        stationAi.AiDrone = component.AiDrone;
        stationAi.LastDroneSpawn = component.LastDroneSpawn;
        stationAi.Core = component.Core;
        EnsureComp<SiliconLawBoundComponent>(drone);
        EnsureComp<StationAiDroneComponent>(drone).AiCore = aiCore;

        _entityManager.InitializeAndStartEntity(drone, coords.GetMapId(_entityManager));
        _metaDataSystem.SetEntityName(drone, metaName != "" ? metaName : "Invalid AI");

        if (TryComp<BrokenAiComponent>(uid, out _))
        {
            _entityManager.AddComponent<BrokenAiComponent>(drone);
        }

        _transformSystem.AttachToGridOrMap(drone);
    }

    private void CanInteraction(Entity<StationAIComponent> ent, ref InteractionAttemptEvent args)
    {
        if (HasComp<StationAiDroneComponent>(ent.Owner))
            return;

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

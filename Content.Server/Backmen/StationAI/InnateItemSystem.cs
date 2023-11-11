using Content.Shared.Actions;
using Content.Shared.Backmen.StationAI.Events;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.StationAI;

public sealed partial class InnateItemSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InnateItemComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<InnateItemComponent, InnateAfterInteractActionEvent>(StartAfterInteract);
        SubscribeLocalEvent<InnateItemComponent, InnateBeforeInteractActionEvent>(StartBeforeInteract);
    }

    private void OnMindAdded(EntityUid uid, InnateItemComponent component, MindAddedMessage args)
    {
        if (!component.AlreadyInitialized)
            RefreshItems(uid, component);

        component.AlreadyInitialized = true;
    }

    private void RefreshItems(EntityUid uid, InnateItemComponent component)
    {
        int priority = component.StartingPriority ?? 0;
        foreach (var (key, sourceItem) in component.Slots)
        {
            /*if (_tagSystem.HasTag(sourceItem, "NoAction"))
                continue;*/

            var actionId = component.Actions.FirstOrNull(x => x.Key == key)?.Value;
            _actionsSystem.AddAction(uid, ref actionId, sourceItem);
            component.Actions[key] = actionId!.Value;
            if (_actionsSystem.TryGetActionData(actionId, out var action))
            {
                action.Priority = priority;
                Dirty(actionId.Value, action);
            }
            priority--;
        }
    }

    private void StartAfterInteract(EntityUid uid, InnateItemComponent component, InnateAfterInteractActionEvent args)
    {
        EnsureItem(uid, component, args.Item);
        if (!component.Items.ContainsKey(args.Item))
            return;
        var ev = new AfterInteractEvent(args.Performer, component.Items[args.Item], args.Target, Transform(args.Target).Coordinates, true);
        RaiseLocalEvent(component.Items[args.Item], ev, false);
    }

    private void StartBeforeInteract(EntityUid uid, InnateItemComponent component, InnateBeforeInteractActionEvent args)
    {
        EnsureItem(uid, component, args.Item);
        if (!component.Items.ContainsKey(args.Item))
            return;
        var ev = new BeforeRangedInteractEvent(args.Performer, component.Items[args.Item], args.Target, Transform(args.Target).Coordinates, true);
        RaiseLocalEvent(component.Items[args.Item], ev, false);
    }

    private void EnsureItem(EntityUid uid, InnateItemComponent component, EntProtoId args)
    {
        if (!component.Items.ContainsKey(args) && args != "")
        {
            if (!TrySpawnInContainer(args, uid, "tools", out var item))
            {
                Logger.Error($"Невозможно заспавнить tools {args}");
                return;
            }
            component.Items[args] = item.Value;
        }
    }
}



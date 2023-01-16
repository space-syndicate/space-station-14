using Content.Server.Corvax.Sponsors;
using Content.Server.Speech.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.OwOAction;

public sealed class OwOActionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager EntityManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SponsorsManager _sponsorsManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, OwOAccentActionEvent>(OnOwOAction);
        SubscribeLocalEvent<OwOActionComponent, OwOAccentActionEvent>(OnChange);
        SubscribeLocalEvent<OwOActionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OwOActionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnChange(EntityUid uid, OwOActionComponent component, OwOAccentActionEvent args)
    {
        component.IsON = !component.IsON;
    }

    private void OnShutdown(EntityUid uid, OwOActionComponent component, ComponentShutdown args)
    {
        if(component.OwOAction != null)
            _actionsSystem.RemoveAction(uid, component.OwOAction);
    }

    private void OnStartup(EntityUid uid, OwOActionComponent component, ComponentStartup args)
    {
        if(component.OwOAction == null && _proto.TryIndex(component.ActionId, out InstantActionPrototype? act))
        {
            component.OwOAction = new(act);
        }

        if(component.OwOAction != null)
            _actionsSystem.AddAction(uid, component.OwOAction, null);
    }

    private void OnOwOAction(EntityUid uid, MobStateComponent component, OwOAccentActionEvent ev)
    {

        if (ev.Handled)
            return;

        var enabled = EntityManager.HasComponent<OwOAccentComponent>(uid);

        if (enabled)
            EntityManager.RemoveComponent<OwOAccentComponent>(uid);
        else
            EntityManager.AddComponent<OwOAccentComponent>(uid);

        ev.Handled = true;
    }
}

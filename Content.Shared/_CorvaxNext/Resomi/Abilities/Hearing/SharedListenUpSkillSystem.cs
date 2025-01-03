using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;

namespace Content.Shared._CorvaxNext.Resomi.Abilities.Hearing;

public abstract class SharedListenUpSkillSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] protected readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ListenUpSkillComponent, ListenUpActionEvent>(OnActivateListenUp);
        SubscribeLocalEvent<ListenUpSkillComponent, ListenUpDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<ListenUpSkillComponent, MoveInputEvent>(OnMoveInput);
    }
    private void OnActivateListenUp(Entity<ListenUpSkillComponent> ent, ref ListenUpActionEvent args)
    {

        var doAfterArgs = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.prepareTime, new ListenUpDoAfterEvent(), ent.Owner, null, null)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.01f
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }
    private void OnDoAfter(Entity<ListenUpSkillComponent> ent, ref ListenUpDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.toggled)
            return;

        AddComp<ListenUpComponent>(ent.Owner);

        _actionsSystem.SetToggled(ent.Comp.SwitchListenUpActionEntity, true);
        ent.Comp.toggled = !ent.Comp.toggled;
    }

    private void OnMoveInput(Entity<ListenUpSkillComponent> ent, ref MoveInputEvent args)
    {
        if (!ent.Comp.toggled)
            return;

        RemComp<ListenUpComponent>(ent.Owner);

        _actionsSystem.SetToggled(ent.Comp.SwitchListenUpActionEntity, false);
        ent.Comp.toggled = !ent.Comp.toggled;
    }
}

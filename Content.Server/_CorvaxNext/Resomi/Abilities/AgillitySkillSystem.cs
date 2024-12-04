using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Maps;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared._CorvaxNext.Resomi;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared._CorvaxNext.Resomi.Abilities;
using Content.Shared.Damage.Components;
using Robust.Shared.Physics;

namespace Content.Server._CorvaxNext.Resomi.Abilities;

public sealed class AgillitySkillSystem : SharedAgillitySkillSystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;

    private Entity<BaseActionComponent> action;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AgillitySkillComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<AgillitySkillComponent, SwitchAgillityActionEvent>(SwitchAgility);
        SubscribeLocalEvent<AgillitySkillComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnComponentInit(Entity<AgillitySkillComponent> ent, ref ComponentInit args)
    {
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.SwitchAgilityActionEntity, ent.Comp.SwitchAgilityAction, ent.Owner);
    }

    private void SwitchAgility(Entity<AgillitySkillComponent> ent, ref SwitchAgillityActionEvent args)
    {
        action = args.Action;

        if (!ent.Comp.Active)
        {
            ActivateAgility(ent, action);
        }
        else
        {
            DeactivateAgility(ent.Owner, ent.Comp, action);
        }
    }

    private void ActivateAgility(Entity<AgillitySkillComponent> ent, Entity<BaseActionComponent> action)
    {
        if (!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var comp))
            return;

        _popup.PopupEntity(Loc.GetString("agility-activated-massage"), ent.Owner, ent.Owner);

        ent.Comp.SprintSpeedCurrent += ent.Comp.SprintSpeedModifier; // adding a modifier to the base running speed
        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent.Owner);

        ent.Comp.Active = !ent.Comp.Active;

        var ev = new SwitchAgillity(action, ent.Comp.Active);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    private void DeactivateAgility(EntityUid uid, AgillitySkillComponent component, Entity<BaseActionComponent> action)
    {
        if (!TryComp<MovementSpeedModifierComponent>(uid, out var comp))
            return;

        _popup.PopupEntity(Loc.GetString("agility-deactivated-massage"), uid, uid);

        component.SprintSpeedCurrent = 1f; // return the base running speed to normal
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);

        _actions.SetCooldown(action.Owner, component.CooldownDelay);

        component.Active = !component.Active;

        var ev = new SwitchAgillity(action, component.Active);
        RaiseLocalEvent(uid, ref ev);
    }

    private void OnRefreshMovespeed(Entity<AgillitySkillComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(1f, ent.Comp.SprintSpeedCurrent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AgillitySkillComponent>();
        while (query.MoveNext(out var uid, out var resomiComp))
        {
            if (!TryComp<StaminaComponent>(uid, out var stamina)
                || !resomiComp.Active
                || Timing.CurTime < resomiComp.NextUpdateTime)
                continue;

            resomiComp.NextUpdateTime = Timing.CurTime + resomiComp.UpdateRate;

            _stamina.TryTakeStamina(uid, resomiComp.StaminaDamagePassive);
            if (stamina.StaminaDamage > stamina.CritThreshold * 0.50f)
                DeactivateAgility(uid, resomiComp, action);
        }
    }
}

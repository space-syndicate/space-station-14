using Content.Server.Administration.Logs;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Medical.Components;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Audio;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.Medical;

public sealed class SurgerySystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StackSystem _stacks = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SurgeryToolComponent, AfterInteractEvent>(OnSurgeryAfterInteract);
        SubscribeLocalEvent<DamageableComponent, SurgeryDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<DamageableComponent> entity, ref SurgeryDoAfterEvent args)
    {
        TryComp<SurgeryToolComponent>(entity, out var comp);
        var dontRepeat = false;
        if (!TryComp(args.Used, out SurgeryToolComponent? healing))
            return;
        if (args.Handled || args.Cancelled)
            return;
        if (healing.DamageContainers is not null &&
            entity.Comp.DamageContainerID is not null &&
            !healing.DamageContainers.Contains(entity.Comp.DamageContainerID))
        {
            return;
        }
        var healed = _damageable.TryChangeDamage(entity.Owner, healing.Damage, true, origin: args.Args.User);
        if (healed == null)
            return;
        var total = healed?.GetTotal() ?? FixedPoint2.Zero;
        if (entity.Owner != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{EntityManager.ToPrettyString(args.User):user} healed {EntityManager.ToPrettyString(entity.Owner):target} for {total:damage} damage");
        }
        _audio.PlayPvs(comp!.InProgressSound, args.User, AudioHelpers.WithVariation(0.125f, _random).WithVolume(1f));

        args.Repeat = (HasDamage(entity.Comp, healing) && !dontRepeat);
        if (!args.Repeat && !dontRepeat)
            args.Handled = true;
    }

    private bool HasDamage(DamageableComponent component, SurgeryToolComponent healing)
    {
        var damageableDict = component.Damage.DamageDict;
        var healingDict = healing.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict[type.Key].Value > 0)
            {
                return true;
            }
        }

        return false;
    }
    private void OnSurgeryAfterInteract(Entity<SurgeryToolComponent> entity, ref AfterInteractEvent args)
    {
        TryComp<SurgeryToolComponent>(entity, out var comp);
        var target = (EntityUid)args.Target!;
        TryComp<ActiveSurgeryComponent>(args.Target, out var surgery);
        if (args.Handled || !args.CanReach || args.Target == null && !HasComp<ActiveSurgeryComponent>(args.Target) && surgery!.IsActive != true)
            return;

        if (entity.Comp.IsScalpel == true && surgery!.IsActive != true)
        {
            surgery!.IsActive = true;
            _audio.PlayPvs(comp!.ScalpelSound, entity.Owner);
            args.Handled = true;
        }

        if (surgery!.IsActive == true)
        {
            if (entity.Comp.IsCautery == true)
            {
            _audio.PlayPvs(comp!.CauterySound, entity.Owner);
            RemComp<ActiveSurgeryComponent>(target);
            args.Handled = true;
            }
        
            if (entity.Comp.IsHemostat == true)
            {
            if (TryHeal(entity, args.User, args.Target!.Value, entity.Comp))
                args.Handled = true;
            }
        }
        
    }

    public bool TryHeal(EntityUid uid, EntityUid user, EntityUid target, SurgeryToolComponent component)
    {
        if (!TryComp<DamageableComponent>(target, out var targetDamage))
            return false;
        TryComp<BuckleComponent>(target, out var buckle);
        if (!buckle!.Buckled)
            return false;
        if (component.DamageContainers is not null &&
            targetDamage.DamageContainerID is not null &&
            !component.DamageContainers.Contains(targetDamage.DamageContainerID))
        {
            return false;
        }
        if (user != target && !_interactionSystem.InRangeUnobstructed(user, target, popup: true))
            return false;
        var anythingToDo = HasDamage(targetDamage, component);
        if (!anythingToDo)
        {
            return false;
        }
        _audio.PlayPvs(component!.InProgressSound, user, AudioHelpers.WithVariation(0.125f, _random).WithVolume(1f));
        var delay = component.Delay;
        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay, new SurgeryDoAfterEvent(), target, target: target, used: uid)
            {
                // Didn't break on damage as they may be trying to prevent it and
                // not being able to heal your own ticking damage would be frustrating.
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };
        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }
}

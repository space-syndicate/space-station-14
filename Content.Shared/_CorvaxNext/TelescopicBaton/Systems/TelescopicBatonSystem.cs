using Content.Shared.Damage.Events;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Standing;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Stunnable;

namespace Content.Shared._CorvaxNext.TelescopicBaton;

public sealed class TelescopicBatonSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedLayingDownSystem _layingDown = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelescopicBatonComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TelescopicBatonComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
        SubscribeLocalEvent<TelescopicBatonComponent, ItemToggledEvent>(ToggleDone);
        SubscribeLocalEvent<TelescopicBatonComponent, StaminaMeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<TelescopicBatonComponent> ent, ref StaminaMeleeHitEvent args)
    {
        foreach (var (uid, _) in args.HitList)
        {
            if (TryComp<LayingDownComponent>(uid, out var layingDownComponent))
            {
                _layingDown.TryLieDown(uid, layingDownComponent, null, DropHeldItemsBehavior.NoDrop);
            }
        }
    }

    private void OnStaminaHitAttempt(Entity<TelescopicBatonComponent> entity, ref StaminaDamageOnHitAttemptEvent args)
    {
        if (!_itemToggle.IsActivated(entity.Owner))
            args.Cancelled = true;
    }

    private void OnExamined(Entity<TelescopicBatonComponent> entity, ref ExaminedEvent args)
    {
        var onMsg = _itemToggle.IsActivated(entity.Owner)
            ? Loc.GetString("comp-telescopicbaton-examined-on")
            : Loc.GetString("comp-telescopicbaton-examined-off");
        args.PushMarkup(onMsg);
    }

    private void ToggleDone(Entity<TelescopicBatonComponent> entity, ref ItemToggledEvent args)
    {
        _item.SetHeldPrefix(entity.Owner, args.Activated ? "on" : "off");
    }
}

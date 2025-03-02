using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using System.Numerics;
using Content.Shared._White;
using Content.Shared._CorvaxNext.Standing;
using Content.Shared.Standing;

namespace Content.Shared._White.Grab;

public sealed class GrabThrownSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedLayingDownSystem _layingDown = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrabThrownComponent, StartCollideEvent>(HandleCollide);
        SubscribeLocalEvent<GrabThrownComponent, StopThrowEvent>(OnStopThrow);
    }

    private void HandleCollide(Entity<GrabThrownComponent> ent, ref StartCollideEvent args)
    {
        if (_netMan.IsClient) // To avoid effect spam
            return;

        if (!HasComp<ThrownItemComponent>(ent.Owner))
        {
            RemComp<GrabThrownComponent>(ent.Owner);
            return;
        }

        if (ent.Comp.IgnoreEntity.Contains(args.OtherEntity))
            return;

        if (!HasComp<DamageableComponent>(ent.Owner))
            RemComp<GrabThrownComponent>(ent.Owner);

        ent.Comp.IgnoreEntity.Add(args.OtherEntity);

        var speed = args.OurBody.LinearVelocity.Length();

        if (ent.Comp.StaminaDamageOnCollide != null)
            _stamina.TakeStaminaDamage(ent.Owner, ent.Comp.StaminaDamageOnCollide.Value);

        var damageScale = speed;

        if (ent.Comp.WallDamageOnCollide != null)
            _damageable.TryChangeDamage(args.OtherEntity, ent.Comp.WallDamageOnCollide * damageScale);

        _layingDown.TryLieDown(args.OtherEntity, behavior: DropHeldItemsBehavior.AlwaysDrop);

        _color.RaiseEffect(Color.Red, new List<EntityUid>() { ent.Owner }, Filter.Pvs(ent.Owner, entityManager: EntityManager));
    }

    private void OnStopThrow(EntityUid uid, GrabThrownComponent comp, StopThrowEvent args)
    {
        if (comp.DamageOnCollide != null)
            _damageable.TryChangeDamage(uid, comp.DamageOnCollide);

        if (HasComp<GrabThrownComponent>(uid))
            RemComp<GrabThrownComponent>(uid);
    }

    /// <summary>
    /// Throwing entity to the direction and ensures GrabThrownComponent with params
    /// </summary>
    /// <param name="uid">Entity to throw</param>
    /// <param name="thrower">Entity that throws</param>
    /// <param name="vector">Direction</param>
    /// <param name="staminaDamage">Stamina damage on collide</param>
    /// <param name="damageToUid">Damage to entity on collide</param>
    /// <param name="damageToWall">Damage to wall or anything that was hit by entity</param>
    public void Throw(
        EntityUid uid,
        EntityUid thrower,
        Vector2 vector,
        float grabThrownSpeed,
        float? staminaDamage = null,
        DamageSpecifier? damageToUid = null,
        DamageSpecifier? damageToWall = null)
    {
        _layingDown.TryLieDown(uid, behavior: DropHeldItemsBehavior.AlwaysDrop);

        var comp = EnsureComp<GrabThrownComponent>(uid);
        comp.StaminaDamageOnCollide = staminaDamage;
        comp.DamageOnCollide = damageToUid;
        comp.WallDamageOnCollide = damageToWall;
        comp.IgnoreEntity.Add(thrower);

        _throwing.TryThrow(uid, vector, grabThrownSpeed, animated: false);
    }
}

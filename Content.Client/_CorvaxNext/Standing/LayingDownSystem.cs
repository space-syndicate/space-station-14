using Content.Shared.ActionBlocker;
using Content.Shared._CorvaxNext.NextVars;
using Content.Shared._CorvaxNext.Standing;
using Content.Shared.Buckle;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CorvaxNext.Standing;

public sealed class LayingDownSystem : SharedLayingDownSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedRotationVisualsSystem _rotationVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LayingDownComponent, MoveEvent>(OnMovementInput);
        SubscribeLocalEvent<LayingDownComponent, AfterAutoHandleStateEvent>(OnChangeDraw);
        SubscribeLocalEvent<StandingStateComponent, AfterAutoHandleStateEvent>(OnChangeStanding);

        _cfg.OnValueChanged(NextVars.AutoGetUp, b => _autoGetUp = b, true);

        //SubscribeNetworkEvent<CheckAutoGetUpEvent>(OnCheckAutoGetUp);
    }

    private void OnChangeStanding(Entity<StandingStateComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_animation.HasRunningAnimation(ent, "rotate"))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        if (ent.Comp.Standing)
        {
            sprite.Rotation = Angle.Zero;
            return;
        }

        if (sprite.Rotation != Angle.FromDegrees(270) && sprite.Rotation != Angle.FromDegrees(90))
        {
            sprite.Rotation = Angle.FromDegrees(270);
        }
    }

    private void OnChangeDraw(Entity<LayingDownComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (ent.Comp.DrawDowned)
        {
            sprite.DrawDepth = (int)Shared.DrawDepth.DrawDepth.SmallMobs;
        }
        else if (!ent.Comp.DrawDowned && sprite.DrawDepth == (int)Shared.DrawDepth.DrawDepth.SmallMobs)
        {
            sprite.DrawDepth = (int)Shared.DrawDepth.DrawDepth.Mobs;
        }
    }

    private bool _autoGetUp;

    protected override bool GetAutoGetUp(Entity<LayingDownComponent> ent, ICommonSession session)
    {
        return _autoGetUp;
    }

    private void OnMovementInput(EntityUid uid, LayingDownComponent component, MoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        if (!_standing.IsDown(uid) || _animation.HasRunningAnimation(uid, "rotate") || _buckle.IsBuckled(uid))
            return;
        if (TerminatingOrDeleted(uid))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite)
            || !TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
        {
            return;
        }

        ProcessVisuals((uid, Transform(uid), sprite, rotationVisuals));
    }

    private void ProcessVisuals(Entity<TransformComponent, SpriteComponent?, RotationVisualsComponent> entity)
    {
        var rotation = entity.Comp1.LocalRotation + (_eyeManager.CurrentEye.Rotation - (entity.Comp1.LocalRotation - _transform.GetWorldRotation(entity.Comp1)));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            _rotationVisuals.SetHorizontalAngle((entity.Owner, entity.Comp3), Angle.FromDegrees(270));
            if (entity.Comp2 != null)
                entity.Comp2.Rotation = Angle.FromDegrees(270);
            return;
        }

        _rotationVisuals.ResetHorizontalAngle((entity.Owner, entity.Comp3));
        if (entity.Comp2 != null)
            entity.Comp2.Rotation = entity.Comp3.DefaultRotation;
    }

    public override void AutoGetUp(Entity<LayingDownComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        var transform = Transform(ent);

        if (!TryComp<RotationVisualsComponent>(ent, out var rotationVisuals))
            return;

        ProcessVisuals((ent.Owner, transform, null, rotationVisuals));
    }

    /*
    private void OnCheckAutoGetUp(CheckAutoGetUpEvent ev, EntitySessionEventArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var uid = GetEntity(ev.User);

        if (!TryComp<TransformComponent>(uid, out var transform) || !TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
            return;

        var rotation = transform.LocalRotation + (_eyeManager.CurrentEye.Rotation - (transform.LocalRotation - transform.WorldRotation));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            rotationVisuals.HorizontalRotation = Angle.FromDegrees(270);
            return;
        }

        rotationVisuals.HorizontalRotation = Angle.FromDegrees(90);
    }*/
}

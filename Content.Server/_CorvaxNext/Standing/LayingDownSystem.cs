using Content.Shared._CorvaxNext.NextVars;
using Content.Shared._CorvaxNext.Standing;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._CorvaxNext.Standing;

public sealed class LayingDownSystem : SharedLayingDownSystem // WD EDIT
{
    [Dependency] private readonly INetConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedRotationVisualsSystem _rotationVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeNetworkEvent<CheckAutoGetUpEvent>(OnCheckAutoGetUp);
        SubscribeLocalEvent<LayingDownComponent, StoodEvent>(OnStoodEvent);
        SubscribeLocalEvent<LayingDownComponent, DownedEvent>(OnDownedEvent);
    }
    private void OnDownedEvent(Entity<LayingDownComponent> ent, ref DownedEvent args)
    {
        // Raising this event will lower the entity's draw depth to the same as a small mob.
        if (CrawlUnderTables)
        {
            ent.Comp.DrawDowned = true;
            Dirty(ent, ent.Comp);
        }
    }

    private void OnStoodEvent(Entity<LayingDownComponent> ent, ref StoodEvent args)
    {
        if (CrawlUnderTables)
        {
            ent.Comp.DrawDowned = false;
            Dirty(ent, ent.Comp);
        }
    }

    public override void AutoGetUp(Entity<LayingDownComponent> ent)
    {
        if (!TryComp<EyeComponent>(ent, out var eyeComp) || !TryComp<RotationVisualsComponent>(ent, out var rotationVisualsComp))
            return;

        var xform = Transform(ent);

        var rotation = xform.LocalRotation + (eyeComp.Rotation - (xform.LocalRotation - _transform.GetWorldRotation(xform)));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            _rotationVisuals.SetHorizontalAngle((ent, rotationVisualsComp), Angle.FromDegrees(270));
            return;
        }

        _rotationVisuals.ResetHorizontalAngle((ent, rotationVisualsComp));
    }

    protected override bool GetAutoGetUp(Entity<LayingDownComponent> ent, ICommonSession session)
    {
        return _cfg.GetClientCVar(session.Channel, NextVars.AutoGetUp);
    }
    /*
        private void OnCheckAutoGetUp(CheckAutoGetUpEvent ev, EntitySessionEventArgs args)
        {
            var uid = GetEntity(ev.User);

            if (!TryComp(uid, out LayingDownComponent? layingDown))
                return;

            layingDown.AutoGetUp = _cfg.GetClientCVar(args.SenderSession.Channel, CCVars.AutoGetUp);
            Dirty(uid, layingDown);
        }
        */
}

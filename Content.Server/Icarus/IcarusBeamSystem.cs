using Content.Server.Body.Components;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Player;

namespace Content.Server.Icarus;

public sealed class IcarusBeamSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<IcarusBeamComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<IcarusBeamComponent, StartCollideEvent>(OnCollide);
    }

    private void OnComponentInit(EntityUid uid, IcarusBeamComponent component, ComponentInit args)
    {
        if (EntityManager.TryGetComponent(uid, out PhysicsComponent? phys))
        {
            phys.LinearDamping = 0f;
            phys.Friction = 0f;
            phys.BodyStatus = BodyStatus.InAir;

            var xform = Transform(uid);
            var vel = new Vector2(component.Speed, 0);

            phys.ApplyLinearImpulse(vel);
            xform.LocalRotation = (vel - xform.WorldPosition).ToWorldAngle() + MathHelper.PiOver2;
        }

        SoundSystem.Play(component.Sound.GetSound(), Filter.Pvs(uid), uid, AudioParams.Default.WithLoop(true));
    }

    private void OnCollide(EntityUid uid, IcarusBeamComponent component, StartCollideEvent args)
    {
        var ent = args.OtherFixture.Body.Owner;

        // Gib everyone
        if (TryComp<BodyComponent>(ent, out var body))
            body.Gib();

        QueueDel(ent);
    }
}

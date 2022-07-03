namespace Content.Server.Icarus;

/// <summary>
/// This handles...
/// </summary>
public sealed class IcarusBeamSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<IcarusBeamComponent, ComponentInit>(OnComponentInit);
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
    }
}

using Content.Server.SurveillanceCamera;
using Content.Shared.Backmen.StationAI;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class AICameraSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AICameraComponent, ComponentStartup>(HandleCameraStartup);
    }

    private void HandleCameraStartup(EntityUid uid, AICameraComponent component, ComponentStartup args)
    {
        if (!_entityManager.TryGetComponent<SurveillanceCameraComponent>(uid, out var camera))
            return;

        component.CameraName = camera.CameraId;
        component.CameraCategories = camera.AvailableNetworks;
        component.Enabled = true;

        Dirty(uid, component);
    }
}

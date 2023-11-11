using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Events;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class AICameraWarp : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AICameraWarpMessage>(HandleCameraWarpMessage);
    }

    private void HandleCameraWarpMessage(AICameraWarpMessage args)
    {
        var owner = GetEntity(args.Owner);
        // You need to be an AI to do this.
        if (!_entityManager.TryGetComponent<AIEyeComponent>(owner, out var _))
            return;

        var transform = Transform(owner);
        var cameraTransform = Transform(GetEntity(args.Camera));

        if (transform.MapID != cameraTransform.MapID)
            return;

        _transformSystem.SetCoordinates(owner, cameraTransform.Coordinates);
        transform.AttachToGridOrMap();
    }
}

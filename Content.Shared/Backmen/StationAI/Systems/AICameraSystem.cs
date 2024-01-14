using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.StationAI.Systems;

public sealed class AICameraSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AICameraComponent, ComponentGetState>(GetCompState);
        SubscribeLocalEvent<AICameraComponent, ComponentHandleState>(HandleCompState);
    }

    private void GetCompState(EntityUid uid, AICameraComponent component, ref ComponentGetState args)
    {
        args.State = new AICameraComponentState
        {
            Enabled = component.Enabled,
            CameraName = component.CameraName,
            CameraCategories = component.CameraCategories
        };
    }

    private void HandleCompState(EntityUid uid, AICameraComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not AICameraComponentState camera)
            return;

        component.Enabled = camera.Enabled;
        component.CameraName = camera.CameraName;
        component.CameraCategories = camera.CameraCategories;
    }
}

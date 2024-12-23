using Content.Server.SurveillanceCamera;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._CorvaxNext.BodyCam;

/// <summary>
/// A system that automatically enables or disables a camera
/// depending on whether the item is currently equipped in a clothing slot.
/// </summary>
public sealed class BodyCameraSystem : EntitySystem
{
    [Dependency] private readonly SurveillanceCameraSystem _surveillanceSystem = default!;

    public override void Initialize()
    {
        // When the BodyCameraComponent is added, ensure the camera starts off.
        SubscribeLocalEvent<BodyCameraComponent, ComponentStartup>(OnStartup);

        // Turn camera on/off when the clothing item is equipped/unequipped.
        SubscribeLocalEvent<BodyCameraComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<BodyCameraComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    /// <summary>
    /// On component startup, forcibly disable the camera (if found).
    /// </summary>
    private void OnStartup(EntityUid uid, BodyCameraComponent component, ComponentStartup args)
    {
        // If there's a SurveillanceCameraComponent, turn it off immediately.
        if (TryComp<SurveillanceCameraComponent>(uid, out var camComp))
            _surveillanceSystem.SetActive(uid, false, camComp);
    }

    /// <summary>
    /// When the item is equipped, turn the camera on.
    /// </summary>
    private void OnEquipped(EntityUid uid, BodyCameraComponent component, ref ClothingGotEquippedEvent args)
    {
        if (TryComp<SurveillanceCameraComponent>(uid, out var camComp))
            _surveillanceSystem.SetActive(uid, true, camComp);
    }

    /// <summary>
    /// When the item is unequipped, turn the camera off.
    /// </summary>
    private void OnUnequipped(EntityUid uid, BodyCameraComponent component, ref ClothingGotUnequippedEvent args)
    {
        if (TryComp<SurveillanceCameraComponent>(uid, out var camComp))
            _surveillanceSystem.SetActive(uid, false, camComp);
    }
}

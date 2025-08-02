using Content.Shared.Corvax.Ipc;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Corvax.Ipc;

public sealed partial class IpcSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IpcComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<IpcComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(EntityUid uid, IpcComponent comp, ComponentStartup args)
    {
        UpdateFaceSprite(uid, comp);
    }

    private void OnAfterState(EntityUid uid, IpcComponent comp, AfterAutoHandleStateEvent args)
    {
        UpdateFaceSprite(uid, comp);
    }

    private void UpdateFaceSprite(EntityUid uid, IpcComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!sprite.LayerMapTryGet(HumanoidVisualLayers.Snout, out var layer))
            return;

        var profile = _prototypeManager.Index<IpcFaceProfilePrototype>(comp.FaceProfile);
        var rsi = new SpriteSpecifier.Rsi(SpriteSpecifierSerializer.TextureRoot / profile.RsiPath, comp.SelectedFace);
        _sprite.LayerSetSprite(uid, layer, rsi);
    }
}

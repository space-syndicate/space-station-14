using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;


namespace Content.Client._CorvaxNext.Overlays;

public sealed class ListenUpOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private readonly EntityLookupSystem _entityLookup;
    private readonly TransformSystem _transformSystem;
    private readonly IGameTiming _gameTiming;
    private readonly SpriteSystem _spriteSystem;

    private Texture _texture;

    protected float Radius;
    protected SpriteSpecifier Sprite;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ListenUpOverlay(float radius, SpriteSpecifier sprite)
    {
        IoCManager.InjectDependencies(this);

        _transformSystem = _entity.System<TransformSystem>();
        _spriteSystem = _entity.System<SpriteSystem>();
        _entityLookup = _entity.System<EntityLookupSystem>();
        _gameTiming = IoCManager.Resolve<IGameTiming>();

        Radius = radius;
        Sprite = sprite;

        _texture = _spriteSystem.GetFrame(Sprite, _gameTiming.CurTime);


        ZIndex = -1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null
            || _players.LocalEntity == null
            || (!_entity.TryGetComponent<TransformComponent>(_players.LocalEntity, out var playerTransform)))
            return;

        _texture = _spriteSystem.GetFrame(Sprite, _gameTiming.CurTime);

        var handle = args.WorldHandle;
        var eye = args.Viewport.Eye;
        var eyeRot = eye?.Rotation ?? default;

        var entities = _entityLookup.GetEntitiesInRange<MobStateComponent>(playerTransform.Coordinates, Radius);

        foreach (var (uid, stateComp) in entities)
        {

            if (!_entity.TryGetComponent<SpriteComponent>(uid, out var sprite)
             || !sprite.Visible
             || !_entity.TryGetComponent<TransformComponent>(uid, out var xform)
             || (!_entity.TryGetComponent<MobStateComponent>(uid, out var mobstateComp))
             || (mobstateComp.CurrentState != MobState.Alive))
                continue;

            Render((uid, sprite, xform), eye?.Position.MapId, handle, eyeRot);
        }
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void Render(Entity<SpriteComponent, TransformComponent> ent,
                        MapId? map, DrawingHandleWorld handle, Angle eyeRot)
    {
        var (uid, sprite, xform) = ent;

        if (uid == _players.LocalEntity
            || xform.MapID != map)
            return;

        var position = _transformSystem.GetWorldPosition(xform);
        var rotation = Angle.Zero;

        handle.SetTransform(position, rotation);
        handle.DrawTexture(_texture, new System.Numerics.Vector2(-0.5f));
    }
}

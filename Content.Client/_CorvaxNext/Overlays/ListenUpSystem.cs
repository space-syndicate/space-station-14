using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Shared._CorvaxNext.Resomi.Abilities.Hearing;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Utility;
using Robust.Shared.Player;
using Content.Shared.GameTicking;

namespace Content.Client._CorvaxNext.Overlays;

public sealed class ListenUpSystem : SharedListenUpSkillSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;

    private Entity<BaseActionComponent> action;

    private ListenUpOverlay _listenUpOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ListenUpComponent, ComponentStartup>(OnListenStartup);
        SubscribeLocalEvent<ListenUpComponent, ComponentShutdown>(OnListenUpShutdown);

        SubscribeLocalEvent<ListenUpComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ListenUpComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }
    private void OnListenStartup(Entity<ListenUpComponent> ent, ref ComponentStartup args)
    {
        SwithOverlay(ent, true);
    }

    private void OnListenUpShutdown(Entity<ListenUpComponent> ent, ref ComponentShutdown args)
    {
        SwithOverlay(ent, false);
    }

    private void OnPlayerAttached(Entity<ListenUpComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        SwithOverlay(ent, true);
    }

    private void OnPlayerDetached(Entity<ListenUpComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        SwithOverlay(ent, false);
    }
    private void UpdateOverlay(bool active, Overlay overlay)
    {
        if (_player.LocalEntity == null)
        {
            _overlayMan.RemoveOverlay(overlay);
            return;
        }

        if (active)
            _overlayMan.AddOverlay(overlay);
        else
            _overlayMan.RemoveOverlay(overlay);
    }

    private void SwithOverlay(Entity<ListenUpComponent> ent, bool active)
    {
        Overlay overlay = ListenUp(ent.Comp.radius, ent.Comp.Sprite);
        UpdateOverlay(active, overlay);
    }

    private Overlay ListenUp(float radius, SpriteSpecifier sprite)
    {
        _listenUpOverlay = new(radius, sprite);

        return _listenUpOverlay;
    }
}

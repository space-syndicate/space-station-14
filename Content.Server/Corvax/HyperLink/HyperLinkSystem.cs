// Inspired by Nyanotrasen

using Robust.Shared.Player;
using Content.Shared.Interaction;
using Content.Shared.HyperLink;

namespace Content.Server.HyperLink;

public sealed class HyperLinkSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HyperLinkComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, HyperLinkComponent component, ActivateInWorldEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        OpenURL(actor.PlayerSession, component.URL);
    }

    public void OpenURL(ICommonSession session, string url)
    {
        var ev = new OpenURLEvent(url);
        RaiseNetworkEvent(ev, session.Channel);
    }
}

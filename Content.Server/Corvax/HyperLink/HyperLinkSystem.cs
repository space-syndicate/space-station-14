// Inspired by Nyanotrasen

using Robust.Shared.Player;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.HyperLink;

namespace Content.Server.HyperLink;

public sealed class HyperLinkSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HyperLinkComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<HyperLinkComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerb);
    }

    private void OnActivate(EntityUid uid, HyperLinkComponent component, ActivateInWorldEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        OpenURL(actor.PlayerSession, component.URL);
    }

    private void AddAltVerb(EntityUid uid, HyperLinkComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                OpenURL(actor.PlayerSession, component.URL);
            },
            Text = Loc.GetString("book-read-verb"),
            Priority = -2
        };
        args.Verbs.Add(verb);
    }

    public void OpenURL(ICommonSession session, string url)
    {
        var ev = new OpenURLEvent(url);
        RaiseNetworkEvent(ev, session.ConnectedClient);
    }
}
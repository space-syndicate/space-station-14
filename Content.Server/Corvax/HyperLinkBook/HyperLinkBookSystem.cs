// Inspired by Nyanotrasen

using Robust.Shared.Player;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.HyperLinkBook;

namespace Content.Server.HyperLinkBook;

public sealed class HyperLinkBookSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HyperLinkBookComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<HyperLinkBookComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerb);
    }

    private void OnActivate(EntityUid uid, HyperLinkBookComponent component, ActivateInWorldEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        OpenURL(actor.PlayerSession, component.URL);
    }

    private void AddAltVerb(EntityUid uid, HyperLinkBookComponent component, GetVerbsEvent<AlternativeVerb> args)
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
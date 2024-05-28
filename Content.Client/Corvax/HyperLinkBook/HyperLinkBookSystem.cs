// Inspired by Nyanotrasen

using Content.Shared.HyperLinkBooks;
using Robust.Client.UserInterface;

namespace Content.Client.HyperLinkBook;

public sealed class HyperLinkBookSystem : EntitySystem
{
    public override void Initialize() 
    {
        base.Initialize();
        SubscribeNetworkEvent<OpenURLEvent>(OnOpenURL);
    }

    private void OnOpenURL(OpenURLEvent args)
    {
        var uriOpener = IoCManager.Resolve<IUriOpener>();
        uriOpener.OpenUri(args.URL);
    }
}
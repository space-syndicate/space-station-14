using Content.Shared.Corvax.Sponsors;
using Robust.Shared.Network;

namespace Content.Client.Corvax.Sponsors;

public sealed class SponsorsManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    public bool IsSponsor => Info != null;

    public SponsorInfo? Info { get; private set; }

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSponsoringInfo>(HandleSponsoringInfo);
    }

    private void HandleSponsoringInfo(MsgSponsoringInfo message)
    {
        Info = message.Info;
    }
}
using Content.Shared.Corvax.Sponsors;
using Robust.Shared.Network;

namespace Content.Client.Corvax.Sponsors;

public sealed class ClientSponsorsManager : SponsorsManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    public bool IsSponsor = false;
    public string[] AllowedMarkings = Array.Empty<string>();

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSponsoringInfo>(HandleSponsoringInfo);
    }

    private void HandleSponsoringInfo(MsgSponsoringInfo message)
    {
        IsSponsor = message.IsSponsor;
        AllowedMarkings = message.AllowedMarkings;
    }
}

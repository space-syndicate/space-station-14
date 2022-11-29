using System.Diagnostics.CodeAnalysis;
using Content.Shared.Corvax.Sponsors;
using Robust.Shared.Network;

namespace Content.Client.Corvax.Sponsors;

public sealed class SponsorsManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    private SponsorInfo? _info;
    private SponsorInfo[]? _sponsors = null;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSponsorInfo>(msg => _info = msg.Info);
        _netMgr.RegisterNetMessage<MsgSponsorListInfo>(msg => _sponsors = msg.Sponsors);
    }

    public bool TryGetInfo([NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        sponsor = _info;
        return _info != null;
    }

    public bool TryGetSponsorList([NotNullWhen(false)] out SponsorInfo[]? sponsors)
    {
        sponsors = _sponsors;
        return _sponsors != null;
    }
}

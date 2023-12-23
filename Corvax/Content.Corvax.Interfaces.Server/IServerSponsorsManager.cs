using System.Diagnostics.CodeAnalysis;
using Content.Corvax.Interfaces.Shared;
using Robust.Shared.Network;

namespace Content.Corvax.Interfaces.Server;

public interface IServerSponsorsManager : ISharedSponsorsManager
{
    public bool TryGetInfo(NetUserId userId, [NotNullWhen(true)] out ISponsorInfo? sponsor);
}

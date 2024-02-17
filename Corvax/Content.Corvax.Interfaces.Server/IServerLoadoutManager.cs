using System.Diagnostics.CodeAnalysis;
using Content.Corvax.Interfaces.Shared;
using Robust.Shared.Network;

namespace Content.Corvax.Interfaces.Server;

public interface IServerLoadoutManager : ISharedSponsorsManager
{
    public bool TryGetPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes);
}

using System.Diagnostics.CodeAnalysis;
using Content.Corvax.Interfaces.Shared;

namespace Content.Corvax.Interfaces.Client;

public interface IClientSponsorsManager : ISharedSponsorsManager
{
    public bool TryGetInfo([NotNullWhen(true)] out ISponsorInfo? sponsor);
}

using Content.Corvax.Interfaces.Shared;

namespace Content.Corvax.Interfaces.Client;

public interface IClientSponsorsManager : ISharedSponsorsManager
{
    public List<string> Prototypes { get; }
}

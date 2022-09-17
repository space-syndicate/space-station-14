using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Sponsors;

public interface ISponsorsManager
{
    /**
     * Get OOC color of player by their user id from API or if it has already been received before, then from the cache.
     */
    Task<string?> GetSponsorOOCColor(NetUserId userId);
}
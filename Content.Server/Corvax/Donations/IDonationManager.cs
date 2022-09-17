using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Donations;

public interface IDonationManager
{
    /**
     * Get OOC color of player by their user id from API or if it has already been received before, then from the cache.
     */
    Task<string?> GetDonatorOOCColor(NetUserId userId);
}
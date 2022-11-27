using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Shared.Corvax.Sponsors;

[Virtual]
public class SharedSponsorsManager
{
    [Dependency] private readonly MarkingManager _markingMgr = default!;
    [Dependency] private readonly IPrototypeManager _prototypeMgr = default!;

    public string[] GetRestrictedMarkingIds(SponsorInfo? info)
    {
        if (info == null)
            return new string[] { };

        var allMarkings = _prototypeMgr.EnumeratePrototypes<MarkingPrototype>();
    }
}

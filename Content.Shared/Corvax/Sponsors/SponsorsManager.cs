using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;

namespace Content.Shared.Corvax.Sponsors;

[Virtual]
public class SponsorsManager
{
    [Dependency] private readonly MarkingManager _markingMgr = default!;
    
    public void FilterSponsorMarkings(string[] allowedMarkingIds, ICharacterProfile profile)
    {
        if (profile is not HumanoidCharacterProfile humanoid)
            return;
        
        // Hair
        // var hairMarking = new Marking(humanoid.Appearance.HairStyleId, new[] { humanoid.Appearance.HairColor });
        // if (!_markingMgr.TryGetMarking(hairMarking, out var hairProto) && hairProto != null)
        // {
        //     var allowedToHave = allowedMarkingIds.Contains(hairMarking.MarkingId);
        //     if (hairProto.SponsorOnly && !allowedToHave)
        //     {
        //         humanoid.Appearance.HairStyleId = HairStyles.DefaultHairStyle;
        //     }
        // }
        
        // Markings tab
        var toRemove = new List<Marking>();
        foreach (var marking in humanoid.Appearance.Markings)
        {
            if (!_markingMgr.TryGetMarking(marking, out var prototype))
            {
                toRemove.Add(marking);
                continue;
            }

            var allowedToHave = allowedMarkingIds.Contains(marking.MarkingId);
            if (prototype.SponsorOnly && !allowedToHave)
            {
                toRemove.Add(marking);
            }
        }

        foreach (var marking in toRemove)
        {
            humanoid.Appearance.Markings.Remove(marking);
        }
    }
}

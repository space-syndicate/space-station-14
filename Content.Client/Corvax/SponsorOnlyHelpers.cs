using Content.Corvax.Interfaces.Shared;

namespace Content.Client.Corvax;

public static class SponsorOnlyHelpers
{
    public static string GetSponsorOnlySuffix(string prototypeId)
    {
        if (IoCManager.Resolve<ISharedSponsorsManager>() is not { } sponsorsManager)
            return " " + Loc.GetString("sponsor-only-text");

        if (sponsorsManager.TryGetTierNameForPrototype(prototypeId, out var tierName) && !string.IsNullOrEmpty(tierName))
            return " " + Loc.GetString("sponsor-only-tier-text", ("tierName", tierName));

        // Fallback
        return " " + Loc.GetString("sponsor-only-text");
    }
}

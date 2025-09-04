using Robust.Shared.Localization;

namespace Content.Client.Corvax;

public static class SponsorOnlyHelpers
{
    public static string GetSponsorOnlySuffix()
    {
        return " " + Loc.GetString("sponsor-only-text");
    }
}

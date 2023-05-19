using Robust.Shared.Configuration;

namespace Content.Shared.DeadSpace.CCCCVars;

/// <summary>
///     DeadSpace modules console variables
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class CCCCVars
{
    /*
	* GCF
	*/

    /// <summary>
    ///     Whether GCF being shown is enabled at all.
    /// </summary>
    public static readonly CVarDef<bool> GCFEnabled =
        CVarDef.Create("gcf_auto.enabled", true);

    /// <summary>
    ///     Notify for admin about GCF Clean.
    /// </summary>
    public static readonly CVarDef<bool> GCFNotify =
        CVarDef.Create("gcf_auto.notify", false);

    /// <summary>
    ///     The number of seconds between each GCF
    /// </summary>
    public static readonly CVarDef<float> GCFFrequency =
        CVarDef.Create("gcf_auto.frequency", 300f);

    /*
	* InfoLinks
	*/
    /// <summary>
    /// Link to wiki page with roles description in Rules menu.
    /// </summary>
    public static readonly CVarDef<string> InfoLinksRoles =
        CVarDef.Create("infolinks.roles", "", CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Link to wiki page with space laws in Rules menu.
    /// </summary>
    public static readonly CVarDef<string> InfoLinksLaws =
        CVarDef.Create("infolinks.laws", "", CVar.SERVER | CVar.REPLICATED);


    // <summary>
    /// URL of the discord webhook which will relay all bans messages
    // </summary>
    public static readonly CVarDef<string> DiscordBanWebhook =
        CVarDef.Create("discord.ban_webhook", string.Empty, CVar.SERVERONLY);


}
using System.Reflection;
using Content.Server.Discord.DiscordLink;
using NetCord.Gateway;

namespace Content.Server.Corvax.Discord;

/// <summary>
/// Corvax-only access to the in-process Discord gateway client used by the
/// legacy thread bridge. External AHelp API mode does not use this adapter.
/// </summary>
public sealed class AHelpDiscordLinkReflectionAdapter
{
    private readonly DiscordLink _discordLink;

    public AHelpDiscordLinkReflectionAdapter(DiscordLink discordLink)
    {
        _discordLink = discordLink;
    }

    public GatewayClient? GetGatewayClient()
    {
        return _discordLink.GetType()
            .GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(_discordLink) as GatewayClient;
    }
}

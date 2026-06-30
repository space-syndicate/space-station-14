using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.Discord;

public static class AHelpDiscordRelayHelper
{
    public static string BuildDiscordBwoinkText(string authorName, string text)
    {
        var escapedAuthor = FormattedMessage.EscapeText(authorName);
        var escapedText = FormattedMessage.EscapeText(text);
        return $"[color=red]{escapedAuthor} \\[D\\][/color]: {escapedText}";
    }

    public static string GetDiscordRelayName(string authorName)
    {
        return $"{authorName}[D]";
    }

    public static AHelpMessageParams BuildWebhookMessageParams(
        string username,
        string text,
        bool isAdmin,
        string roundTime,
        GameRunLevel runLevel)
    {
        return new AHelpMessageParams(
            username,
            text,
            isAdmin,
            roundTime,
            runLevel,
            playedSound: true);
    }
}

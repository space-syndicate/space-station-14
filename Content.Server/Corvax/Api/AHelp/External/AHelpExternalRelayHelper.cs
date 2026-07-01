using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.Api.AHelp;

public static class AHelpExternalRelayHelper
{
    public static string BuildExternalBwoinkText(string authorName, string text)
    {
        var escapedAuthor = FormattedMessage.EscapeText(authorName);
        var escapedText = FormattedMessage.EscapeText(text);
        return $"[color=red]{escapedAuthor} \\[E\\][/color]: {escapedText}";
    }

    public static string GetExternalRelayName(string authorName)
    {
        return $"{authorName}[E]";
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

using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpExternalApiSystem
{
    private async Task HandleInboundAsync(string json)
    {
        AHelpApiInbound.Base? message;
        try
        {
            message = JsonSerializer.Deserialize<AHelpApiInbound.Base>(json, _jsonOptions);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Invalid Corvax AHelp API message: {e.Message}");
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.Type))
            return;

        switch (message.Type)
        {
            case "ping":
                await SendAsync(new AHelpApiOutbound.Pong(message.RequestId));
                break;
            case "list_players":
                await HandleListPlayersAsync(message.RequestId);
                break;
            case "send_ahelp_message":
                await HandleSendAHelpMessageAsync(json, message.RequestId);
                break;
            case "open_ahelp":
                await HandleOpenAHelpAsync(json, message.RequestId);
                break;
            default:
                await SendErrorAsync(message.RequestId, $"Unsupported message type '{message.Type}'");
                break;
        }
    }

    private async Task HandleListPlayersAsync(string? requestId)
    {
        var players = await RunOnMainThread(() => _playerManager.Sessions
            .OrderBy(session => session.Name)
            .Select(BuildPlayerInfo)
            .ToArray());

        await SendAsync(new AHelpApiOutbound.PlayersResponse(requestId, players));
    }

    private async Task HandleSendAHelpMessageAsync(string json, string? requestId)
    {
        var message = JsonSerializer.Deserialize<AHelpApiInbound.SendAHelpMessage>(json, _jsonOptions);
        if (message == null || string.IsNullOrWhiteSpace(message.Text))
        {
            await SendErrorAsync(requestId, "Text is required");
            return;
        }

        if (!Guid.TryParse(message.ConversationId ?? message.UserId, out var userGuid))
        {
            await SendErrorAsync(requestId, "conversationId or userId must be a NetUserId guid");
            return;
        }

        var userId = new NetUserId(userGuid);
        var authorName = string.IsNullOrWhiteSpace(message.AuthorName)
            ? message.AuthorDiscordId ?? "Discord"
            : message.AuthorName;
        var plainText = message.Text.ReplaceLineEndings(" ");

        var result = await RunOnMainThread(() =>
        {
            if (!_bwoinkAdapter.HasActiveConversation(userId))
                return "AHelp conversation is not active";

            RelayDiscordMessageToAHelp(userId, authorName, plainText);
            return string.Empty;
        });

        if (!string.IsNullOrEmpty(result))
            await SendErrorAsync(requestId, result);
        else
            await SendOkAsync(requestId);
    }

    private async Task HandleOpenAHelpAsync(string json, string? requestId)
    {
        var message = JsonSerializer.Deserialize<AHelpApiInbound.OpenAHelp>(json, _jsonOptions);
        if (message == null || string.IsNullOrWhiteSpace(message.Ckey) || string.IsNullOrWhiteSpace(message.Text))
        {
            await SendErrorAsync(requestId, "ckey and text are required");
            return;
        }

        var result = await RunOnMainThread(() =>
        {
            if (!TryGetSessionByCkey(message.Ckey, out var target))
                return $"Player '{message.Ckey}' not found";

            var authorName = string.IsNullOrWhiteSpace(message.AuthorName)
                ? message.AuthorDiscordId ?? "Discord"
                : message.AuthorName;
            RelayDiscordMessageToAHelp(target.UserId, authorName, message.Text.ReplaceLineEndings(" "));
            return string.Empty;
        });

        if (!string.IsNullOrEmpty(result))
            await SendErrorAsync(requestId, result);
        else
            await SendOkAsync(requestId);
    }
}

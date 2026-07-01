using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Api.AHelp;

public sealed partial class AHelpExternalApiSystem
{
    private async Task<bool> HandleInboundAsync(string json)
    {
        if (!_enabled)
            return false;

        AHelpApiInbound.Base? message;
        try
        {
            message = JsonSerializer.Deserialize<AHelpApiInbound.Base>(json, _jsonOptions);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Invalid Corvax AHelp API message: {e.Message}");
            return false;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.Type))
            return false;

        switch (message.Type)
        {
            case "ping":
                await SendAsync(new AHelpApiOutbound.Pong(message.RequestId));
                return true;
            case "list_players":
                await HandleListPlayersAsync(message.RequestId);
                return true;
            case "send_ahelp_message":
                await HandleSendAHelpMessageAsync(json, message.RequestId);
                return true;
            case "open_ahelp":
                await HandleOpenAHelpAsync(json, message.RequestId);
                return true;
            case "list_objectives":
                await HandleListObjectivesAsync(json, message.RequestId);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleListPlayersAsync(string? requestId)
    {
        var players = await RunOnMainThread(() => _playerManager.Sessions
            .OrderBy(session => session.Name)
            .Select(BuildPlayerInfo)
            .ToArray());

        await SendAsync(new AHelpApiOutbound.PlayersResponse(requestId, true, null, players));
    }

    private async Task HandleSendAHelpMessageAsync(string json, string? requestId)
    {
        if (!TryDeserialize(json, out AHelpApiInbound.SendAHelpMessage? message, out var error))
        {
            await SendErrorAsync(requestId, error);
            return;
        }

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
            ? message.AuthorExternalId ?? "External"
            : message.AuthorName;
        var plainText = message.Text.ReplaceLineEndings(" ");

        var result = await RunOnMainThread(() =>
        {
            if (!HasKnownConversation(userId))
                return "AHelp conversation is not active";

            RelayExternalMessageToAHelp(userId, authorName, plainText);
            return string.Empty;
        });

        if (!string.IsNullOrEmpty(result))
            await SendErrorAsync(requestId, result);
        else
            await SendOkAsync(requestId);
    }

    private async Task HandleOpenAHelpAsync(string json, string? requestId)
    {
        if (!TryDeserialize(json, out AHelpApiInbound.OpenAHelp? message, out var deserializeError))
        {
            await SendErrorAsync(requestId, deserializeError);
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.Ckey) || string.IsNullOrWhiteSpace(message.Text))
        {
            await SendErrorAsync(requestId, "ckey and text are required");
            return;
        }

        NetUserId? openedUserId = null;
        AHelpApiOutbound.ConversationUpsert? upsert = null;
        var error = await RunOnMainThread(() =>
        {
            if (!TryGetSessionByCkey(message.Ckey, out var target))
                return $"Player '{message.Ckey}' not found";

            var authorName = string.IsNullOrWhiteSpace(message.AuthorName)
                ? message.AuthorExternalId ?? "External"
                : message.AuthorName;
            upsert = RememberConversation(target);
            openedUserId = target.UserId;
            RelayExternalMessageToAHelp(target.UserId, authorName, message.Text.ReplaceLineEndings(" "));
            return string.Empty;
        });

        if (!string.IsNullOrEmpty(error))
            await SendErrorAsync(requestId, error);
        else
        {
            await SendAsync(upsert!);
            if (openedUserId is { } userId)
                await RunOnMainThread(() => MarkConversationSent(userId));
            await SendOkAsync(requestId);
        }
    }

    private async Task HandleListObjectivesAsync(string json, string? requestId)
    {
        if (!TryDeserialize(json, out AHelpApiInbound.ListObjectives? message, out var error))
        {
            await SendErrorAsync(requestId, error);
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.Ckey))
        {
            await SendErrorAsync(requestId, "ckey is required");
            return;
        }

        var result = await RunOnMainThread(() =>
        {
            if (!TryGetSessionByCkey(message.Ckey, out var target))
                return (Error: $"Player '{message.Ckey}' not found", Response: (AHelpApiOutbound.ObjectivesResponse?) null);

            if (!_minds.TryGetMind(target, out var mindId, out var mind))
                return (Error: $"Player '{target.Name}' does not have a mind", Response: (AHelpApiOutbound.ObjectivesResponse?) null);

            var objectives = mind.Objectives
                .Select((objective, index) =>
                {
                    var info = _objectives.GetInfo(objective, mindId, mind);
                    return info == null
                        ? new AHelpApiOutbound.ObjectiveInfo(
                            index,
                            objective.ToString(),
                            null,
                            null,
                            0,
                            false)
                        : new AHelpApiOutbound.ObjectiveInfo(
                            index,
                            objective.ToString(),
                            info.Value.Title,
                            info.Value.Description,
                            (int) (info.Value.Progress * 100f),
                            true);
                })
                .ToArray();

            var response = new AHelpApiOutbound.ObjectivesResponse(
                requestId,
                true,
                null,
                target.UserId.ToString(),
                target.Name,
                _minds.GetCharacterName(target.UserId),
                objectives);

            return (Error: string.Empty, Response: (AHelpApiOutbound.ObjectivesResponse?) response);
        });

        if (!string.IsNullOrEmpty(result.Error))
            await SendErrorAsync(requestId, result.Error);
        else
            await SendAsync(result.Response);
    }

    private bool TryDeserialize<T>(string json, out T? message, out string error)
    {
        try
        {
            message = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            error = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            message = default;
            error = $"Invalid payload: {e.Message}";
            return false;
        }
    }
}

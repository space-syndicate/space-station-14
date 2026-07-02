using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.Api.AHelp;

public sealed partial class AHelpExternalApiSystem
{
    private AHelpApiOutbound.PlayerInfo BuildPlayerInfo(ICommonSession session)
    {
        var characterName = _minds.GetCharacterName(session.UserId);
        var job = "-";
        var roleNames = Array.Empty<string>();
        var antagonist = false;

        if (_minds.TryGetMind(session.UserId, out var mind))
        {
            var allRoles = _roles.MindGetAllRoleInfo((mind.Value.Owner, mind.Value.Comp)).ToArray();
            var jobRole = allRoles.FirstOrDefault(role => !role.Antagonist);
            roleNames = allRoles
                .Select(role => Loc.GetString(role.Name))
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(jobRole.Name))
                job = Loc.GetString(jobRole.Name);

            antagonist = allRoles.Any(role => role.Antagonist);
        }

        return new AHelpApiOutbound.PlayerInfo(
            session.UserId.ToString(),
            session.Name,
            session.Status.ToString(),
            characterName,
            job,
            roleNames,
            antagonist);
    }

    private bool TryGetSessionByCkey(string ckey, [NotNullWhen(true)] out ICommonSession? session)
    {
        if (_playerManager.TryGetSessionByUsername(ckey, out session))
            return true;

        session = _playerManager.Sessions.FirstOrDefault(player =>
            string.Equals(player.Name, ckey, StringComparison.OrdinalIgnoreCase));

        return session != null;
    }

    private void RelayExternalMessageToAHelp(NetUserId userId, string authorName, string plainText)
    {
        _bwoinkSystem.CorvaxSendAHelpToGame(userId, BuildExternalBwoinkText(authorName, plainText));
        QueueWebhookMessage(userId, $"{authorName}[D]", plainText, isAdmin: true);
    }

    private bool HasKnownConversation(NetUserId userId)
    {
        return _knownExternalConversations.Contains(userId) ||
               _seenRelays.ContainsKey(userId) ||
               _bwoinkSystem.CorvaxHasActiveAHelpConversation(userId);
    }

    private AHelpApiOutbound.ConversationUpsert RememberConversation(ICommonSession session)
    {
        _knownExternalConversations.Add(session.UserId);

        if (!_seenRelays.TryGetValue(session.UserId, out var seen))
            seen = new RelaySeenState(null, Array.Empty<byte>(), false);

        _seenRelays[session.UserId] = seen;
        return BuildConversationUpsert(session);
    }

    private void MarkConversationSent(NetUserId userId)
    {
        if (!_seenRelays.TryGetValue(userId, out var seen))
            seen = new RelaySeenState(null, Array.Empty<byte>(), false);

        _seenRelays[userId] = seen with { ConversationSent = true };
    }

    private void QueueWebhookMessage(NetUserId userId, string username, string text, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var messageParams = new AHelpMessageParams(
            username,
            text,
            isAdmin,
            _gameTicker.RunLevel == GameRunLevel.InRound
                ? _gameTicker.RoundDuration().ToString("hh\\:mm\\:ss")
                : string.Empty,
            _gameTicker.RunLevel,
            playedSound: true);

        _bwoinkSystem.CorvaxQueueAHelpWebhookMessage(userId, messageParams);
    }

    private static string BuildExternalBwoinkText(string authorName, string text)
    {
        return $"[color=red]{FormattedMessage.EscapeText(authorName)} \\[D\\][/color]: {FormattedMessage.EscapeText(text)}";
    }

    private async Task SendOkAsync(string? requestId)
    {
        await SendAsync(new AHelpApiOutbound.Response(requestId, true, null));
    }

    private async Task SendErrorAsync(string? requestId, string error)
    {
        await SendAsync(new AHelpApiOutbound.Response(requestId, false, error));
    }

    private async Task SendAsync<T>(T payload)
    {
        await _corvaxApi.SendAsync(payload);
    }

    private void RunLoggedAsync(Task task, string operation)
    {
        _ = RunLoggedAsyncCore(task, operation);
    }

    private async Task RunLoggedAsyncCore(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Shutdown and reconnect paths may cancel pending API work.
        }
        catch (Exception e)
        {
            _sawmill.Error($"Corvax AHelp API failed to {operation}: {e}");
        }
    }

    private async Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        return await tcs.Task;
    }

    private async Task RunOnMainThread(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        await tcs.Task;
    }
}

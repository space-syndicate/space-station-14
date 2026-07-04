using System.Linq;
using Content.Server.GameTicking;
using Robust.Shared.Network;

namespace Content.Server.Administration.Systems;

public sealed partial class BwoinkSystem
{
    internal bool CorvaxHasActiveAHelpConversation(NetUserId userId)
    {
        return _relayMessages.ContainsKey(userId);
    }

    internal IReadOnlyList<CorvaxAHelpRelaySnapshot> CorvaxGetAHelpRelaySnapshots()
    {
        return _relayMessages
            .Select(pair => new CorvaxAHelpRelaySnapshot(
                pair.Key,
                pair.Value.Id,
                _webhookData?.ChannelId,
                pair.Value.Username,
                pair.Value.CharacterName,
                pair.Value.Description,
                pair.Value.LastRunLevel))
            .ToArray();
    }

    internal void CorvaxSendAHelpToGame(NetUserId userId, string text)
    {
        var admins = GetTargetAdmins();
        var bwoinkMessage = new BwoinkTextMessage(
            userId,
            SystemUserId,
            text,
            sentAt: DateTime.Now,
            playSound: true);

        foreach (var admin in admins)
        {
            RaiseNetworkEvent(bwoinkMessage, admin);
        }

        if (_playerManager.TryGetSessionById(userId, out var session) && !admins.Contains(session.Channel))
            RaiseNetworkEvent(bwoinkMessage, session.Channel);
    }

    internal bool CorvaxQueueAHelpWebhookMessage(NetUserId userId, AHelpMessageParams parameters)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
            return false;

        if (!_messageQueues.TryGetValue(userId, out var queue))
        {
            queue = new Queue<DiscordRelayedData>();
            _messageQueues[userId] = queue;
        }

        queue.Enqueue(GenerateAHelpMessage(parameters));
        return true;
    }
}

internal sealed record CorvaxAHelpRelaySnapshot(
    NetUserId UserId,
    string? RootMessageId,
    string? WebhookChannelId,
    string Username,
    string? CharacterName,
    string Description,
    GameRunLevel LastRunLevel);

internal sealed class CorvaxAHelpRelayChangedEvent(NetUserId userId) : EntityEventArgs
{
    public NetUserId UserId { get; } = userId;
}

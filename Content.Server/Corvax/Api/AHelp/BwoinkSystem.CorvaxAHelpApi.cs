using System.Linq;
using Content.Server.GameTicking;
using Robust.Shared.Network;

namespace Content.Server.Administration.Systems;

public sealed partial class BwoinkSystem
{
    internal ulong? CorvaxGetAHelpWebhookChannelId()
    {
        return ulong.TryParse(_webhookData?.ChannelId, out var channelId)
            ? channelId
            : null;
    }

    internal bool CorvaxHasActiveAHelpConversation(NetUserId userId)
    {
        return _relayMessages.ContainsKey(userId);
    }

    internal IReadOnlyList<CorvaxAHelpRelaySnapshot> CorvaxGetAHelpRelaySnapshots()
    {
        return _relayMessages
            .Select(pair => new CorvaxAHelpRelaySnapshot(
                pair.Key,
                ulong.TryParse(pair.Value.Id, out var rootMessageId) ? rootMessageId : null,
                pair.Value.Username,
                pair.Value.CharacterName,
                pair.Value.Description,
                pair.Value.LastRunLevel))
            .ToArray();
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
    ulong? RootMessageId,
    string Username,
    string? CharacterName,
    string Description,
    GameRunLevel LastRunLevel);

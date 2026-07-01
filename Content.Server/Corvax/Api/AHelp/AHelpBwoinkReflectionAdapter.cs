using System.Collections;
using System.Linq;
using System.Reflection;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Api.AHelp;

/// <summary>
/// Corvax-only adapter around private <see cref="BwoinkSystem"/> relay state.
/// The upstream system does not expose hooks for webhook message ids,
/// so the external AHelp API keeps all reflection here instead of spreading it
/// through the bridge and API transport implementations.
/// </summary>
public sealed class AHelpBwoinkReflectionAdapter
{
    private readonly BwoinkSystem _bwoinkSystem;

    public AHelpBwoinkReflectionAdapter(BwoinkSystem bwoinkSystem)
    {
        _bwoinkSystem = bwoinkSystem;
    }

    public bool TryGetAHelpWebhookChannelId(out ulong channelId)
    {
        channelId = default;

        var webhookData = GetPrivateFieldValue(_bwoinkSystem, "_webhookData");
        if (webhookData == null)
            return false;

        var channelIdValue = webhookData.GetType()
            .GetProperty("ChannelId", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(webhookData) as string;

        return ulong.TryParse(channelIdValue, out channelId);
    }

    public bool HasActiveConversation(NetUserId userId)
    {
        return GetRelaySnapshots().Any(snapshot => snapshot.UserId == userId);
    }

    public IReadOnlyList<AHelpRelaySnapshot> GetRelaySnapshots()
    {
        var relayMessages = GetPrivateFieldValue(_bwoinkSystem, "_relayMessages") as IEnumerable;
        if (relayMessages == null)
            return Array.Empty<AHelpRelaySnapshot>();

        var snapshots = new List<AHelpRelaySnapshot>();
        foreach (var entry in relayMessages)
        {
            var entryType = entry.GetType();
            var key = entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
            if (key is not NetUserId userId)
                continue;

            var value = entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
            if (value == null)
                continue;

            var interactionType = value.GetType();
            var id = interactionType.GetField("Id", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string;
            var rootMessageId = ulong.TryParse(id, out var parsedId) ? parsedId : (ulong?) null;
            var username = interactionType.GetField("Username", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string ?? string.Empty;
            var characterName = interactionType.GetField("CharacterName", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string;
            var description = interactionType.GetField("Description", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string ?? string.Empty;
            var lastRunLevel = interactionType.GetField("LastRunLevel", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) is GameRunLevel runLevel
                ? runLevel
                : GameRunLevel.PreRoundLobby;

            snapshots.Add(new AHelpRelaySnapshot(userId, rootMessageId, username, characterName, description, lastRunLevel));
        }

        return snapshots;
    }

    public bool QueueWebhookMessage(NetUserId userId, AHelpMessageParams parameters)
    {
        if (string.IsNullOrWhiteSpace(GetPrivateFieldValue(_bwoinkSystem, "_webhookUrl") as string))
            return false;

        var discordRelayedData = _bwoinkSystem.GetType()
            .GetMethod("GenerateAHelpMessage", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_bwoinkSystem, new object[] { parameters });

        if (discordRelayedData == null)
            return false;

        var messageQueues = GetPrivateFieldValue(_bwoinkSystem, "_messageQueues") as IDictionary;
        if (messageQueues == null)
            return false;

        if (!messageQueues.Contains(userId))
        {
            var queueType = messageQueues.GetType().GetGenericArguments()[1];
            messageQueues[userId] = Activator.CreateInstance(queueType);
        }

        var queue = messageQueues[userId];
        queue?.GetType()
            .GetMethod("Enqueue", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(queue, new[] { discordRelayedData });

        return true;
    }

    private static object? GetPrivateFieldValue(object instance, string fieldName)
    {
        return instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(instance);
    }
}

public sealed record AHelpRelaySnapshot(
    NetUserId UserId,
    ulong? RootMessageId,
    string Username,
    string? CharacterName,
    string Description,
    GameRunLevel LastRunLevel);

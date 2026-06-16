using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpDiscordThreadBridgeSystem
{
    private async Task HandleRootWebhookMessageAsync(Message message)
    {
        if (!TryGetAHelpWebhookChannelId(out var webhookChannelId) || message.ChannelId != webhookChannelId)
            return;

        _sawmill.Debug($"Received root ahelp webhook message {message.Id} in channel {message.ChannelId}, scheduling thread creation.");
        await TryCreateThreadForRootMessageAsync(message.Id, message.Author.Username);
    }

    private bool TryGetAHelpWebhookChannelId(out ulong channelId)
    {
        channelId = default;

        var webhookData = GetPrivateFieldValue(_bwoinkSystem, "_webhookData");
        if (webhookData == null)
            return false;

        var channelIdValue = webhookData.GetType().GetProperty("ChannelId", BindingFlags.Instance | BindingFlags.Public)?.GetValue(webhookData) as string;
        return ulong.TryParse(channelIdValue, out channelId);
    }

    private bool TryGetRelayUserByMessageId(ulong messageId, out NetUserId userId, out string username, out string? characterName)
    {
        userId = default;
        username = string.Empty;
        characterName = null;

        var relayMessages = GetPrivateFieldValue(_bwoinkSystem, "_relayMessages") as IEnumerable;
        if (relayMessages == null)
            return false;

        foreach (var entry in relayMessages)
        {
            var entryType = entry.GetType();
            var key = entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
            if (key is not NetUserId currentUserId)
                continue;

            var value = entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
            if (value == null)
                continue;

            var interactionType = value.GetType();
            var id = interactionType.GetField("Id", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string;
            if (!ulong.TryParse(id, out var relayMessageId) || relayMessageId != messageId)
                continue;

            userId = currentUserId;
            username = interactionType.GetField("Username", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string ?? string.Empty;
            characterName = interactionType.GetField("CharacterName", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string;
            return true;
        }

        return false;
    }

    private async Task TryCreateThreadForRootMessageAsync(ulong rootMessageId, string authorUsername)
    {
        if (!TryGetAHelpWebhookChannelId(out var webhookChannelId))
            return;

        if (IsRootMessageProcessed(rootMessageId))
            return;

        NetUserId userId = default;
        string username = string.Empty;
        string? characterName = null;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (TryGetRelayUserByMessageId(rootMessageId, out userId, out username, out characterName))
                break;

            await Task.Delay(100);
        }

        if (userId == default && TryTakePendingThreadRequest(authorUsername, out var pending))
        {
            userId = pending.UserId;
            username = pending.Username;
            characterName = pending.CharacterName;
            _sawmill.Debug($"Matched root ahelp webhook message {rootMessageId} to pending Discord !ah request for {username}.");
        }

        if (userId == default || string.IsNullOrEmpty(username) && characterName == null)
            return;

        if (TryGetThreadForUser(userId, out _))
        {
            MarkRootMessageProcessed(rootMessageId);
            return;
        }

        if (!TryBeginThreadCreation(userId))
            return;

        try
        {
            var threadName = BuildThreadName(username, characterName);
            var thread = await CreateThreadFromMessageAsync(webhookChannelId, rootMessageId, threadName);
            if (thread == null)
                return;

            lock (_stateLock)
            {
                _userThreads[userId] = thread.Id;
                _threadUsers[thread.Id] = userId;
                _createdRootMessages.Add(rootMessageId);
            }

            await JoinThreadAsync(thread.Id);
        }
        finally
        {
            EndThreadCreation(userId);
        }
    }

    private async Task EnsureThreadForUserFromRelayAsync(NetUserId userId)
    {
        try
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                if (TryGetThreadForUser(userId, out _))
                    return;

                if (TryGetRelayMessageForUser(userId, out var rootMessageId, out var username, out var characterName))
                {
                    await TryCreateThreadForKnownUserAsync(rootMessageId, userId, username, characterName);
                    return;
                }

                await Task.Delay(100);
            }

            _sawmill.Warning($"Timed out waiting for ahelp webhook relay message id for {userId}; Discord thread was not created.");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while ensuring Discord ahelp thread for {userId}: {e}");
        }
    }

    private async Task TryCreateThreadForKnownUserAsync(ulong rootMessageId, NetUserId userId, string username, string? characterName)
    {
        if (!TryGetAHelpWebhookChannelId(out var webhookChannelId))
            return;

        if (IsRootMessageProcessed(rootMessageId) || TryGetThreadForUser(userId, out _))
            return;

        if (!TryBeginThreadCreation(userId))
            return;

        try
        {
            var threadName = BuildThreadName(username, characterName);
            var thread = await CreateThreadFromMessageAsync(webhookChannelId, rootMessageId, threadName);
            if (thread == null)
                return;

            lock (_stateLock)
            {
                if (_userThreads.ContainsKey(userId))
                    return;

                _userThreads[userId] = thread.Id;
                _threadUsers[thread.Id] = userId;
                _createdRootMessages.Add(rootMessageId);
            }

            await JoinThreadAsync(thread.Id);
        }
        finally
        {
            EndThreadCreation(userId);
        }
    }

    private bool TryGetRelayMessageForUser(NetUserId userId, out ulong messageId, out string username, out string? characterName)
    {
        messageId = default;
        username = string.Empty;
        characterName = null;

        var relayMessages = GetPrivateFieldValue(_bwoinkSystem, "_relayMessages") as IEnumerable;
        if (relayMessages == null)
            return false;

        foreach (var entry in relayMessages)
        {
            var entryType = entry.GetType();
            var key = entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
            if (key is not NetUserId currentUserId || currentUserId != userId)
                continue;

            var value = entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
            if (value == null)
                return false;

            var interactionType = value.GetType();
            var id = interactionType.GetField("Id", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string;
            if (!ulong.TryParse(id, out messageId))
                return false;

            username = interactionType.GetField("Username", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string ?? string.Empty;
            characterName = interactionType.GetField("CharacterName", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) as string;
            return !string.IsNullOrEmpty(username) || characterName != null;
        }

        return false;
    }

    private async Task<GuildThread?> CreateThreadFromMessageAsync(ulong channelId, ulong messageId, string threadName)
    {
        var client = GetPrivateFieldValue(_discordLink, "_client") as GatewayClient;
        if (client == null)
            return null;

        try
        {
            return await client.Rest.CreateGuildThreadAsync(
                channelId,
                messageId,
                new GuildThreadFromMessageProperties(threadName)
                {
                    AutoArchiveDuration = ThreadArchiveDuration.OneDay,
                    Slowmode = 0,
                },
                default,
                default);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to create Discord thread from ahelp message: {e}");
            return null;
        }
    }

    private async Task JoinThreadAsync(ulong threadId)
    {
        var client = GetPrivateFieldValue(_discordLink, "_client") as GatewayClient;
        if (client == null)
            return;

        try
        {
            await client.Rest.JoinGuildThreadAsync(threadId, default, default);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to join Discord thread {threadId}: {e}");
        }
    }

    private static string BuildThreadName(string username, string? characterName)
    {
        var baseName = string.IsNullOrWhiteSpace(characterName)
            ? username
            : $"{username} ({characterName})";

        baseName = baseName.Trim();
        if (baseName.Length > 80)
            baseName = baseName[..80];

        return $"ahelp: {baseName}";
    }
}

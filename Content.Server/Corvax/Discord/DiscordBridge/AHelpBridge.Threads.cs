using System;
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
        var webhookChannelId = await _taskManager.RunOnMainThreadAsync(() =>
            TryGetAHelpWebhookChannelId(out var channelId) ? channelId : 0);
        if (webhookChannelId == 0 || message.ChannelId != webhookChannelId)
            return;

        _sawmill.Debug($"Received root ahelp webhook message {message.Id} in channel {message.ChannelId}, scheduling thread creation.");
        await TryCreateThreadForRootMessageAsync(message.Id, message.Author.Username);
    }

    private bool TryGetAHelpWebhookChannelId(out ulong channelId)
    {
        return _bwoinkAdapter.TryGetAHelpWebhookChannelId(out channelId);
    }

    private bool TryGetRelayUserByMessageId(ulong messageId, out NetUserId userId, out string username, out string? characterName)
    {
        return _bwoinkAdapter.TryGetRelayUserByMessageId(messageId, out userId, out username, out characterName);
    }

    private async Task TryCreateThreadForRootMessageAsync(ulong rootMessageId, string authorUsername)
    {
        var webhookChannelId = await _taskManager.RunOnMainThreadAsync(() =>
            TryGetAHelpWebhookChannelId(out var channelId) ? channelId : 0);
        if (webhookChannelId == 0)
            return;

        if (IsRootMessageProcessed(rootMessageId))
            return;

        NetUserId userId = default;
        string username = string.Empty;
        string? characterName = null;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var snapshot = await _taskManager.RunOnMainThreadAsync(() =>
                TryGetRelayUserByMessageId(rootMessageId, out var relayUserId, out var relayUsername, out var relayCharacterName)
                    ? new RelayMessageLookup(relayUserId, relayUsername, relayCharacterName)
                    : null);

            if (snapshot != null)
            {
                userId = snapshot.UserId;
                username = snapshot.Username;
                characterName = snapshot.CharacterName;
                break;
            }

            await Task.Delay(100);
        }

        if (userId == default)
        {
            var pending = await _taskManager.RunOnMainThreadAsync(() =>
                TryTakePendingThreadRequest(authorUsername, out var request) ? request : null);
            if (pending != null)
            {
                userId = pending.UserId;
                username = pending.Username;
                characterName = pending.CharacterName;
                _sawmill.Debug($"Matched root ahelp webhook message {rootMessageId} to pending Discord !ah request for {username}.");
            }
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

                var snapshot = await _taskManager.RunOnMainThreadAsync(() =>
                    TryGetRelayMessageForUser(userId, out var rootMessageId, out var username, out var characterName)
                        ? new RelayMessageLookup(userId, username, characterName, rootMessageId)
                        : null);

                if (snapshot != null)
                {
                    await TryCreateThreadForKnownUserAsync(
                        snapshot.RootMessageId!.Value,
                        userId,
                        snapshot.Username,
                        snapshot.CharacterName);
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
        var webhookChannelId = await _taskManager.RunOnMainThreadAsync(() =>
            TryGetAHelpWebhookChannelId(out var channelId) ? channelId : 0);
        if (webhookChannelId == 0)
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
        return _bwoinkAdapter.TryGetRelayMessageForUser(userId, out messageId, out username, out characterName);
    }

    private async Task<GuildThread?> CreateThreadFromMessageAsync(ulong channelId, ulong messageId, string threadName)
    {
        var client = _discordLinkAdapter.GetGatewayClient();
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
        var client = _discordLinkAdapter.GetGatewayClient();
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

    private sealed record RelayMessageLookup(
        NetUserId UserId,
        string Username,
        string? CharacterName,
        ulong? RootMessageId = null);
}

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Content.Server.Discord;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Discord.DiscordLink;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System.Threading.Tasks;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpDiscordThreadBridgeSystem : SharedBwoinkSystem
{
    [Dependency] private DiscordLink _discordLink = default!;
    [Dependency] private BwoinkSystem _bwoinkSystem = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    private ISawmill _sawmill = default!;

    private readonly object _stateLock = new();
    private readonly Dictionary<NetUserId, ulong> _userThreads = new();
    private readonly Dictionary<ulong, NetUserId> _threadUsers = new();
    private readonly HashSet<ulong> _createdRootMessages = new();
    private readonly Queue<PendingThreadRequest> _pendingThreadRequests = new();
    private readonly HttpClient _httpClient = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("corvax.ahelp.thread");
        _discordLink.OnMessageReceived += OnDiscordMessageReceived;

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearState());
    }

    public override void Shutdown()
    {
        _discordLink.OnMessageReceived -= OnDiscordMessageReceived;
        ClearState();
        base.Shutdown();
    }

    private void ClearState()
    {
        lock (_stateLock)
        {
            _userThreads.Clear();
            _threadUsers.Clear();
            _createdRootMessages.Clear();
            _pendingThreadRequests.Clear();
        }
    }

    private void OnDiscordMessageReceived(Message message)
    {
        _taskManager.RunOnMainThread(() => HandleDiscordMessageOnMainThread(message));
    }

    private async void HandleDiscordMessageOnMainThread(Message message)
    {
        try
        {
            if (message.WebhookId != null)
            {
                await HandleRootWebhookMessageAsync(message);
                return;
            }

            if (message.Author.IsBot)
                return;

            if (TryGetUserForThread(message.ChannelId, out var threadUserId))
            {
                if (await TryHandleThreadCommandAsync(message))
                    return;

                RelayDiscordMessageToGame(threadUserId, message);
                RelayDiscordMessageToWebhookQueue(threadUserId, message);
                return;
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while processing Discord ahelp bridge message: {e}");
        }
    }

    private async Task<bool> TryHandleThreadCommandAsync(Message message)
    {
        var content = message.Content.Trim();
        if (!content.StartsWith('!'))
            return false;

        if (content.Equals("!ckey", StringComparison.OrdinalIgnoreCase))
        {
            await SendPlayerListAsync(message.ChannelId);
            return true;
        }

        if (content.StartsWith("!ah ", StringComparison.OrdinalIgnoreCase))
        {
            var args = content["!ah ".Length..].Trim();
            await HandleOpenAHelpCommandAsync(message.ChannelId, message, args);
            return true;
        }

        return false;
    }

    private async Task SendPlayerListAsync(ulong channelId)
    {
        var sessions = _playerManager.Sessions
            .Where(session => session.Status != SessionStatus.Disconnected)
            .OrderBy(session => session.Name)
            .ToArray();

        if (sessions.Length == 0)
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, "На сервере нет подключенных игроков.");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("```");
        builder.AppendLine("ckey | персонаж | должность | роли | антаг");

        foreach (var session in sessions)
        {
            var characterName = _minds.GetCharacterName(session.UserId) ?? "-";
            var job = "-";
            var roleNames = "-";
            var antagonist = "нет";

            if (_minds.TryGetMind(session.UserId, out var mind))
            {
                var mindEntity = mind.Value;
                var roles = _roles.MindGetAllRoleInfo((mindEntity.Owner, mindEntity.Comp));
                var jobRole = roles.FirstOrDefault(role => !role.Antagonist);
                var namedRoles = roles
                    .Select(role => Loc.GetString(role.Name))
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .ToArray();

                if (!string.IsNullOrWhiteSpace(jobRole.Name))
                    job = Loc.GetString(jobRole.Name);

                if (namedRoles.Length != 0)
                    roleNames = string.Join(", ", namedRoles);

                antagonist = roles.Any(role => role.Antagonist) ? "да" : "нет";
            }

            builder.AppendLine($"{session.Name} | {characterName} | {job} | {roleNames} | {antagonist}");
        }

        builder.AppendLine("```");

        foreach (var chunk in SplitDiscordMessage(builder.ToString()))
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, chunk);
        }
    }

    private async Task HandleOpenAHelpCommandAsync(ulong channelId, Message message, string args)
    {
        if (!TryParseAHelpCommand(args, out var ckey, out var ahelpText))
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, "Использование: `!ah ckey сообщение`");
            return;
        }

        if (!TryGetSessionByCkey(ckey, out var target))
        {
            await SendDiscordThreadWebhookMessageAsync(channelId, $"Игрок `{ckey}` не найден на сервере.");
            return;
        }

        var authorName = message.Author.GlobalName ?? message.Author.Username;
        var relayName = GetDiscordRelayName(authorName);
        var plainText = ahelpText.ReplaceLineEndings(" ");
        var bwoinkText = BuildDiscordBwoinkText(authorName, plainText);

        SendAHelpToGame(target.UserId, bwoinkText);
        RegisterPendingThreadRequest(target);
        QueueAHelpWebhookMessage(
            target.UserId,
            relayName,
            plainText,
            isAdmin: true);

        _ = EnsureThreadForUserFromRelayAsync(target.UserId);

        await SendDiscordThreadWebhookMessageAsync(channelId, $"AH для `{target.Name}` отправлен");
    }

    private static bool TryParseAHelpCommand(string args, out string ckey, out string message)
    {
        ckey = string.Empty;
        message = string.Empty;

        var split = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]))
            return false;

        ckey = split[0];
        message = split[1];
        return true;
    }

    private bool TryGetSessionByCkey(string ckey, [NotNullWhen(true)] out ICommonSession? session)
    {
        if (_playerManager.TryGetSessionByUsername(ckey, out session))
            return true;

        session = _playerManager.Sessions.FirstOrDefault(player =>
            string.Equals(player.Name, ckey, StringComparison.OrdinalIgnoreCase));

        return session != null;
    }

    private void SendAHelpToGame(NetUserId userId, string text)
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
        {
            RaiseNetworkEvent(bwoinkMessage, session.Channel);
        }
    }

    private async Task SendDiscordThreadWebhookMessageAsync(ulong channelId, string message)
    {
        if (!TryGetAHelpWebhookUrl(out var webhookUrl))
            return;

        try
        {
            var payload = new WebhookPayload
            {
                Content = message,
                Username = "AHelp",
            };

            var request = await _httpClient.PostAsync(
                $"{webhookUrl}?wait=true&thread_id={channelId}",
                new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json"));

            if (!request.IsSuccessStatusCode)
            {
                var content = await request.Content.ReadAsStringAsync();
                _sawmill.Error($"Discord returned bad status code when posting ahelp thread webhook response: {request.StatusCode}\nResponse: {content}");
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to send Discord ahelp thread webhook response: {e}");
        }
    }

    private static IEnumerable<string> SplitDiscordMessage(string message)
    {
        const int maxLength = 1900;

        if (message.Length <= maxLength)
            return new[] { message };

        var chunks = new List<string>();
        var remaining = message;

        while (remaining.Length > maxLength)
        {
            var splitAt = remaining.LastIndexOf('\n', maxLength);
            if (splitAt <= 0)
                splitAt = maxLength;

            chunks.Add(remaining[..splitAt]);
            remaining = remaining[splitAt..].TrimStart();
        }

        if (remaining.Length != 0)
            chunks.Add(remaining);

        return chunks;
    }

    private async Task HandleRootWebhookMessageAsync(Message message)
    {
        if (!TryGetAHelpWebhookChannelId(out var webhookChannelId) || message.ChannelId != webhookChannelId)
            return;

        _sawmill.Debug($"Received root ahelp webhook message {message.Id} in channel {message.ChannelId}, scheduling thread creation.");
        await TryCreateThreadForRootMessageAsync(message.Id);
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

    private bool TryGetThreadForUser(NetUserId userId, out ulong threadId)
    {
        lock (_stateLock)
        {
            return _userThreads.TryGetValue(userId, out threadId);
        }
    }

    private async Task TryCreateThreadForRootMessageAsync(ulong rootMessageId)
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

        if (userId == default && TryTakePendingThreadRequest(out var pending))
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

    private bool TryGetUserForThread(ulong threadId, out NetUserId userId)
    {
        lock (_stateLock)
        {
            return _threadUsers.TryGetValue(threadId, out userId);
        }
    }

    private bool IsRootMessageProcessed(ulong rootMessageId)
    {
        lock (_stateLock)
        {
            return _createdRootMessages.Contains(rootMessageId);
        }
    }

    private void MarkRootMessageProcessed(ulong rootMessageId)
    {
        lock (_stateLock)
        {
            _createdRootMessages.Add(rootMessageId);
        }
    }

    private void RegisterPendingThreadRequest(ICommonSession target)
    {
        lock (_stateLock)
        {
            RemoveExpiredPendingThreadRequests();
            _pendingThreadRequests.Enqueue(new PendingThreadRequest(
                target.UserId,
                target.Name,
                _minds.GetCharacterName(target.UserId),
                DateTime.UtcNow));
        }
    }

    private bool TryTakePendingThreadRequest([NotNullWhen(true)] out PendingThreadRequest? request)
    {
        lock (_stateLock)
        {
            RemoveExpiredPendingThreadRequests();

            while (_pendingThreadRequests.TryDequeue(out var pending))
            {
                if (!TryGetThreadForUser(pending.UserId, out _))
                {
                    request = pending;
                    return true;
                }
            }
        }

        request = null;
        return false;
    }

    private void RemoveExpiredPendingThreadRequests()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(30);

        while (_pendingThreadRequests.Count > 0 && _pendingThreadRequests.Peek().CreatedAt < cutoff)
        {
            _pendingThreadRequests.Dequeue();
        }
    }

    private void RelayDiscordMessageToGame(NetUserId userId, Message message)
    {
        var authorName = message.Author.GlobalName ?? message.Author.Username;
        var text = message.Content.ReplaceLineEndings(" ");

        if (string.IsNullOrWhiteSpace(text))
            return;

        var admins = GetTargetAdmins();
        var bwoinkText = BuildDiscordBwoinkText(authorName, text);
        var bwoinkMessage = new BwoinkTextMessage(
            userId,
            SystemUserId,
            bwoinkText,
            sentAt: DateTime.Now,
            playSound: true);

        foreach (var admin in admins)
        {
            RaiseNetworkEvent(bwoinkMessage, admin);
        }

        if (_playerManager.TryGetSessionById(userId, out var session) && !admins.Contains(session.Channel))
        {
            RaiseNetworkEvent(bwoinkMessage, session.Channel);
        }
    }

    private void RelayDiscordMessageToWebhookQueue(NetUserId userId, Message message)
    {
        if (!TryGetAHelpWebhookUrl(out _))
            return;

        var authorName = message.Author.GlobalName ?? message.Author.Username;
        var text = message.Content.ReplaceLineEndings(" ");
        if (string.IsNullOrWhiteSpace(text))
            return;

        QueueAHelpWebhookMessage(userId, GetDiscordRelayName(authorName), text, isAdmin: true);
    }

    private static string BuildDiscordBwoinkText(string authorName, string text)
    {
        var escapedAuthor = FormattedMessage.EscapeText(authorName);
        var escapedText = FormattedMessage.EscapeText(text);
        return $"[color=red]{escapedAuthor} \\[D\\][/color]: {escapedText}";
    }

    private static string GetDiscordRelayName(string authorName)
    {
        return $"{authorName} [D]";
    }

    private void QueueAHelpWebhookMessage(NetUserId userId, string username, string text, bool isAdmin)
    {
        if (!TryGetAHelpWebhookUrl(out _))
            return;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var roundTime = _gameTicker.RunLevel == GameRunLevel.InRound
            ? _gameTicker.RoundDuration().ToString("hh\\:mm\\:ss")
            : string.Empty;

        var messageParams = new AHelpMessageParams(
            username,
            text,
            isAdmin,
            roundTime,
            _gameTicker.RunLevel,
            playedSound: true);

        var discordRelayedData = GenerateAHelpMessage(messageParams);
        if (discordRelayedData == null)
            return;

        var messageQueues = GetPrivateFieldValue(_bwoinkSystem, "_messageQueues") as IDictionary;
        if (messageQueues == null)
            return;

        if (!messageQueues.Contains(userId))
        {
            var queueType = messageQueues.GetType().GetGenericArguments()[1];
            messageQueues[userId] = Activator.CreateInstance(queueType);
        }

        var queue = messageQueues[userId];
        queue?.GetType()
            .GetMethod("Enqueue", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(queue, new[] { discordRelayedData });
    }

    private object? GenerateAHelpMessage(AHelpMessageParams parameters)
    {
        return _bwoinkSystem.GetType()
            .GetMethod("GenerateAHelpMessage", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_bwoinkSystem, new object[] { parameters });
    }

    private bool TryGetAHelpWebhookUrl(out string webhookUrl)
    {
        webhookUrl = GetPrivateFieldValue(_bwoinkSystem, "_webhookUrl") as string ?? string.Empty;
        return !string.IsNullOrWhiteSpace(webhookUrl);
    }

    private IList<INetChannel> GetTargetAdmins()
    {
        return _adminManager.ActiveAdmins
            .Where(p => _adminManager.GetAdminData(p)?.HasFlag(AdminFlags.Adminhelp) ?? false)
            .Select(p => p.Channel)
            .ToList();
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

    private static object? GetPrivateFieldValue(object instance, string fieldName)
    {
        return instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(instance);
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

    private sealed record PendingThreadRequest(
        NetUserId UserId,
        string Username,
        string? CharacterName,
        DateTime CreatedAt);
}

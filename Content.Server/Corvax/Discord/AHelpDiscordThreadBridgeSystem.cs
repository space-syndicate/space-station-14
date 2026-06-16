using System;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Discord.DiscordLink;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using NetCord.Gateway;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

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

                await RelayDiscordMessageToGame(threadUserId, message);
                await RelayDiscordMessageToWebhookQueue(threadUserId, message);
                return;
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while processing Discord ahelp bridge message: {e}");
        }
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

    private async Task RelayDiscordMessageToGame(NetUserId userId, Message message)
    {
        var authorName = await GetDiscordAuthorNameAsync(message);
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

    private async Task RelayDiscordMessageToWebhookQueue(NetUserId userId, Message message)
    {
        if (!TryGetAHelpWebhookUrl(out _))
            return;

        var authorName = await GetDiscordAuthorNameAsync(message);
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

    private static async Task<string> GetDiscordAuthorNameAsync(Message message)
    {
        var author = message.Author;

        if (message.Guild != null)
        {
            if (message.Guild.Users.TryGetValue(author.Id, out var cachedGuildUser) &&
                !string.IsNullOrWhiteSpace(cachedGuildUser.Nickname))
            {
                return cachedGuildUser.Nickname;
            }

            try
            {
                var guildUser = await message.Guild.GetUserAsync(author.Id, default, default);
                if (!string.IsNullOrWhiteSpace(guildUser.Nickname))
                    return guildUser.Nickname;
            }
            catch (Exception)
            {
                // Fall back to user-level names if Discord member lookup is temporarily unavailable.
            }
        }

        if (!string.IsNullOrWhiteSpace(author.Username))
            return author.Username;

        if (!string.IsNullOrWhiteSpace(author.GlobalName))
            return author.GlobalName;

        return author.Id.ToString();
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

    private static object? GetPrivateFieldValue(object instance, string fieldName)
    {
        return instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(instance);
    }

}

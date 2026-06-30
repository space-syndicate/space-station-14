using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Discord.DiscordLink;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Corvax.CCCVars;
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
    [Dependency] private IConfigurationManager _cfg = default!;

    private ISawmill _sawmill = default!;
    private bool _isEnabled;
    private bool _externalApiEnabled;
    private bool _subscribedToDiscord;
    private AHelpBwoinkReflectionAdapter _bwoinkAdapter = default!;
    private AHelpDiscordLinkReflectionAdapter _discordLinkAdapter = default!;
    private AHelpDiscordRelayService _relayService = default!;

    private readonly HttpClient _httpClient = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("corvax.ahelp.thread");
        _bwoinkAdapter = new AHelpBwoinkReflectionAdapter(_bwoinkSystem);
        _discordLinkAdapter = new AHelpDiscordLinkReflectionAdapter(_discordLink);
        _relayService = new AHelpDiscordRelayService(
            _adminManager,
            _playerManager,
            _gameTicker,
            _bwoinkAdapter,
            RaiseNetworkEvent);
        _cfg.OnValueChanged(CCCVars.AHelpDiscordThreadBridge, OnEnabledChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiEnabled, OnExternalApiEnabledChanged, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearState());
    }

    public override void Shutdown()
    {
        UnsubscribeDiscord();

        _cfg.UnsubValueChanged(CCCVars.AHelpDiscordThreadBridge, OnEnabledChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiEnabled, OnExternalApiEnabledChanged);
        ClearState();
        base.Shutdown();
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (_isEnabled == enabled)
            return;

        _isEnabled = enabled;

        if (enabled)
        {
            if (_externalApiEnabled)
            {
                _sawmill.Info("Discord ahelp thread bridge is disabled because the central AHelp bot API is enabled.");
                return;
            }

            SubscribeDiscord();
            return;
        }

        _sawmill.Info("Discord ahelp thread bridge is disabled by cvar, not subscribing to Discord messages.");
        UnsubscribeDiscord();
    }

    private void OnExternalApiEnabledChanged(bool enabled)
    {
        _externalApiEnabled = enabled;

        if (!enabled)
        {
            if (_isEnabled)
                SubscribeDiscord();

            return;
        }

        if (_isEnabled)
            UnsubscribeDiscord();

        _sawmill.Info("External Corvax AHelp API enabled; in-process Discord ahelp thread bridge is inactive.");
    }

    private void SubscribeDiscord()
    {
        if (_subscribedToDiscord)
            return;

        _discordLink.OnMessageReceived += OnDiscordMessageReceived;
        _subscribedToDiscord = true;
    }

    private void UnsubscribeDiscord()
    {
        if (!_subscribedToDiscord)
            return;

        _discordLink.OnMessageReceived -= OnDiscordMessageReceived;
        _subscribedToDiscord = false;
    }

    private void OnDiscordMessageReceived(Message message)
    {
        _ = HandleDiscordMessageAsync(message);
    }

    private async Task HandleDiscordMessageAsync(Message message)
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
                var authorName = await GetDiscordAuthorNameAsync(message);
                if (await TryHandleThreadCommandAsync(message, authorName))
                    return;

                await RelayDiscordMessageAsync(threadUserId, authorName, message.Content);
                return;
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while processing Discord ahelp bridge message: {e}");
        }
    }

    private async Task SendDiscordThreadWebhookMessageAsync(ulong channelId, string message)
    {
        var webhookUrl = await _taskManager.RunOnMainThreadAsync(() =>
            _bwoinkAdapter.TryGetAHelpWebhookUrl(out var url) ? url : string.Empty);
        if (string.IsNullOrWhiteSpace(webhookUrl))
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

    private async Task RelayDiscordMessageAsync(NetUserId userId, string authorName, string content)
    {
        var text = content.ReplaceLineEndings(" ");

        if (string.IsNullOrWhiteSpace(text))
            return;

        await _taskManager.RunOnMainThreadAsync(() =>
        {
            _relayService.SendAHelpToGame(userId, AHelpDiscordRelayHelper.BuildDiscordBwoinkText(authorName, text));

            if (_bwoinkAdapter.TryGetAHelpWebhookUrl(out _))
            {
                _relayService.QueueWebhookMessage(
                    userId,
                    AHelpDiscordRelayHelper.GetDiscordRelayName(authorName),
                    text,
                    isAdmin: true);
            }
        });
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

}

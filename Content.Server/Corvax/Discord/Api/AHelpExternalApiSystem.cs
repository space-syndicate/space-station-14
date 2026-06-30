using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpExternalApiSystem : SharedBwoinkSystem
{
    private const string ProtocolVersion = "1";
    private const string Path = "/ahelp/v1/ws";
    private const string AuthScheme = "AHelpToken";
    private const int MaxInboundMessageBytes = 64 * 1024;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IServerDbManager _dbManager = default!;
    [Dependency] private BwoinkSystem _bwoinkSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Dictionary<NetUserId, RelaySeenState> _seenRelays = new();

    private ISawmill _sawmill = default!;
    private AHelpBwoinkReflectionAdapter _bwoinkAdapter = default!;
    private HttpListener? _listener;
    private CancellationTokenSource? _listenCancel;
    private Task? _listenTask;
    private WebSocket? _socket;
    private int _connectionClaimed;
    private int _nextConnectionId;
    private bool _enabled;
    private string _host = "127.0.0.1";
    private int _port = 12120;
    private string _token = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("corvax.ahelp.api");
        _bwoinkAdapter = new AHelpBwoinkReflectionAdapter(_bwoinkSystem);

        _cfg.OnValueChanged(CCCVars.AHelpApiEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiHost, OnHostChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiPort, OnPortChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiToken, OnTokenChanged, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(ev =>
        {
            _seenRelays.Clear();
            _ = SendAsync(new AHelpApiOutbound.RoundChanged(_gameTicker.RoundId, _gameTicker.RunLevel.ToString()));
        });

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(CCCVars.AHelpApiEnabled, OnEnabledChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiHost, OnHostChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiPort, OnPortChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiToken, OnTokenChanged);
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        StopListener();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_socket is not { State: WebSocketState.Open })
            return;

        foreach (var snapshot in _bwoinkAdapter.GetRelaySnapshots())
        {
            var descriptionHash = SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Description));
            if (!_seenRelays.TryGetValue(snapshot.UserId, out var seen))
            {
                seen = new RelaySeenState(null, Array.Empty<byte>());
            }

            if (snapshot.RootMessageId != null && snapshot.RootMessageId != seen.RootMessageId)
            {
                _seenRelays[snapshot.UserId] = seen with { RootMessageId = snapshot.RootMessageId };
                _ = SendConversationUpsertAsync(snapshot);
            }

            if (!descriptionHash.SequenceEqual(seen.DescriptionHash))
            {
                var current = _seenRelays.GetValueOrDefault(snapshot.UserId) ?? seen;
                _seenRelays[snapshot.UserId] = current with { DescriptionHash = descriptionHash };
                _ = SendAsync(new AHelpApiOutbound.AHelpMessage(
                    snapshot.UserId.ToString(),
                    snapshot.UserId.ToString(),
                    "transcript",
                    snapshot.Description,
                    DateTimeOffset.UtcNow));
            }
        }
    }

    private void RestartListener()
    {
        StopListener();

        if (!_enabled)
            return;

        if (string.IsNullOrWhiteSpace(_token))
        {
            _sawmill.Warning("Corvax AHelp API enabled but token is empty; listener will not start.");
            return;
        }

        try
        {
            _listenCancel = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{_host}:{_port}/");
            _listener.Start();
            _listenTask = Task.Run(() => ListenLoop(_listenCancel.Token));
            _sawmill.Info($"Corvax AHelp API listening on ws://{_host}:{_port}{Path}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to start Corvax AHelp API listener: {e}");
            StopListener();
        }
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;
        RestartListener();
    }

    private void OnHostChanged(string value)
    {
        _host = value;
        RestartListener();
    }

    private void OnPortChanged(int value)
    {
        _port = value;
        RestartListener();
    }

    private void OnTokenChanged(string value)
    {
        _token = value;
        RestartListener();
    }

    private void StopListener()
    {
        _listenCancel?.Cancel();
        _listenCancel = null;

        try
        {
            _listener?.Stop();
        }
        catch (Exception)
        {
            // Listener may already be stopped by cancellation.
        }

        _listener = null;

        try
        {
            _socket?.Abort();
            _socket?.Dispose();
        }
        catch (Exception)
        {
            // Best-effort shutdown.
        }

        _socket = null;
        Interlocked.Exchange(ref _connectionClaimed, 0);
        _seenRelays.Clear();
    }

    private async Task ListenLoop(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested && _listener != null)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e)
            {
                if (!cancel.IsCancellationRequested)
                    _sawmill.Error($"Corvax AHelp API listener error: {e}");
                return;
            }

            _ = Task.Run(() => HandleContextAsync(context, cancel), cancel);
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancel)
    {
        if (context.Request.Url?.AbsolutePath != Path)
        {
            await RespondTextAsync(context, HttpStatusCode.NotFound, "Not found");
            return;
        }

        if (!context.Request.IsWebSocketRequest)
        {
            await RespondTextAsync(context, HttpStatusCode.BadRequest, "WebSocket required");
            return;
        }

        if (!CheckAccess(context))
        {
            await RespondTextAsync(context, HttpStatusCode.Unauthorized, "Authorization is invalid");
            return;
        }

        var connectionId = Interlocked.Increment(ref _nextConnectionId);
        if (Interlocked.CompareExchange(ref _connectionClaimed, connectionId, 0) != 0)
        {
            await RespondTextAsync(context, HttpStatusCode.Conflict, "AHelp bot is already connected");
            return;
        }

        WebSocket? acceptedSocket = null;
        try
        {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            acceptedSocket = webSocketContext.WebSocket;
            _socket = acceptedSocket;
            _seenRelays.Clear();
            _sawmill.Info("Corvax AHelp API bot connected.");
            await SendHelloAsync();
            await SendCurrentConversationsAsync();
            await ReceiveLoopAsync(acceptedSocket, cancel);
        }
        catch (Exception e)
        {
            if (!cancel.IsCancellationRequested)
                _sawmill.Error($"Corvax AHelp API connection error: {e}");
        }
        finally
        {
            acceptedSocket?.Dispose();
            if (ReferenceEquals(_socket, acceptedSocket))
            {
                _socket = null;
                _seenRelays.Clear();
                _sawmill.Info("Corvax AHelp API bot disconnected.");
            }

            Interlocked.CompareExchange(ref _connectionClaimed, 0, connectionId);
        }
    }

    private bool CheckAccess(HttpListenerContext context)
    {
        var authHeader = context.Request.Headers["Authorization"];
        if (string.IsNullOrWhiteSpace(authHeader))
            return false;

        var spaceIndex = authHeader.IndexOf(' ');
        if (spaceIndex <= 0)
            return false;

        var scheme = authHeader[..spaceIndex];
        var value = authHeader[(spaceIndex + 1)..].Trim();
        if (!string.Equals(scheme, AuthScheme, StringComparison.Ordinal))
            return false;

        if (string.IsNullOrEmpty(_token))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(value),
            Encoding.UTF8.GetBytes(_token));
    }

    private static async Task RespondTextAsync(HttpListenerContext context, HttpStatusCode statusCode, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.StatusCode = (int) statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken cancel)
    {
        var buffer = new byte[8192];
        while (!cancel.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancel);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Text messages only", cancel);
                    return;
                }

                stream.Write(buffer, 0, result.Count);
                if (stream.Length > MaxInboundMessageBytes)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large", cancel);
                    return;
                }
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(stream.ToArray());
            await HandleInboundAsync(json);
        }
    }

    private async Task HandleInboundAsync(string json)
    {
        AHelpApiInbound.Base? message;
        try
        {
            message = JsonSerializer.Deserialize<AHelpApiInbound.Base>(json, _jsonOptions);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Invalid Corvax AHelp API message: {e.Message}");
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.Type))
            return;

        switch (message.Type)
        {
            case "ping":
                await SendAsync(new AHelpApiOutbound.Pong(message.RequestId));
                break;
            case "list_players":
                await HandleListPlayersAsync(message.RequestId);
                break;
            case "send_ahelp_message":
                await HandleSendAHelpMessageAsync(json, message.RequestId);
                break;
            case "open_ahelp":
                await HandleOpenAHelpAsync(json, message.RequestId);
                break;
            default:
                await SendErrorAsync(message.RequestId, $"Unsupported message type '{message.Type}'");
                break;
        }
    }

    private async Task HandleListPlayersAsync(string? requestId)
    {
        var players = await RunOnMainThread(() => _playerManager.Sessions
            .OrderBy(session => session.Name)
            .Select(BuildPlayerInfo)
            .ToArray());

        await SendAsync(new AHelpApiOutbound.PlayersResponse(requestId, players));
    }

    private async Task HandleSendAHelpMessageAsync(string json, string? requestId)
    {
        var message = JsonSerializer.Deserialize<AHelpApiInbound.SendAHelpMessage>(json, _jsonOptions);
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
            ? message.AuthorDiscordId ?? "Discord"
            : message.AuthorName;
        var plainText = message.Text.ReplaceLineEndings(" ");

        var result = await RunOnMainThread(() =>
        {
            if (!HasActiveConversation(userId))
                return "AHelp conversation is not active";

            SendAHelpToGame(userId, BuildDiscordBwoinkText(authorName, plainText));
            QueueAHelpWebhookMessage(userId, GetDiscordRelayName(authorName), plainText, true);
            return string.Empty;
        });

        if (!string.IsNullOrEmpty(result))
            await SendErrorAsync(requestId, result);
        else
            await SendOkAsync(requestId);
    }

    private async Task HandleOpenAHelpAsync(string json, string? requestId)
    {
        var message = JsonSerializer.Deserialize<AHelpApiInbound.OpenAHelp>(json, _jsonOptions);
        if (message == null || string.IsNullOrWhiteSpace(message.Ckey) || string.IsNullOrWhiteSpace(message.Text))
        {
            await SendErrorAsync(requestId, "ckey and text are required");
            return;
        }

        var result = await RunOnMainThread(() =>
        {
            if (!TryGetSessionByCkey(message.Ckey, out var target))
                return $"Player '{message.Ckey}' not found";

            var targetSession = target;
            var authorName = string.IsNullOrWhiteSpace(message.AuthorName)
                ? message.AuthorDiscordId ?? "Discord"
                : message.AuthorName;
            var plainText = message.Text.ReplaceLineEndings(" ");
            SendAHelpToGame(targetSession.UserId, BuildDiscordBwoinkText(authorName, plainText));
            QueueAHelpWebhookMessage(targetSession.UserId, GetDiscordRelayName(authorName), plainText, true);
            return string.Empty;
        });

        if (!string.IsNullOrEmpty(result))
            await SendErrorAsync(requestId, result);
        else
            await SendOkAsync(requestId);
    }

    private void SendAHelpToGame(NetUserId userId, string text)
    {
        var admins = GetTargetAdmins();
        var bwoinkMessage = new BwoinkTextMessage(userId, SystemUserId, text, sentAt: DateTime.Now, playSound: true);

        foreach (var admin in admins)
        {
            RaiseNetworkEvent(bwoinkMessage, admin);
        }

        if (_playerManager.TryGetSessionById(userId, out var session) && !admins.Contains(session.Channel))
            RaiseNetworkEvent(bwoinkMessage, session.Channel);
    }

    private void QueueAHelpWebhookMessage(NetUserId userId, string username, string text, bool isAdmin)
    {
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

        _bwoinkAdapter.QueueWebhookMessage(userId, messageParams);
    }

    private AHelpApiOutbound.PlayerInfo BuildPlayerInfo(ICommonSession session)
    {
        var characterName = _minds.GetCharacterName(session.UserId);
        var job = "-";
        var roleNames = Array.Empty<string>();
        var antagonist = false;

        if (_minds.TryGetMind(session.UserId, out var mind))
        {
            var roles = _roles.MindGetAllRoleInfo((mind.Value.Owner, mind.Value.Comp)).ToArray();
            var jobRole = roles.FirstOrDefault(role => !role.Antagonist);
            roleNames = roles
                .Select(role => Loc.GetString(role.Name))
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(jobRole.Name))
                job = Loc.GetString(jobRole.Name);

            antagonist = roles.Any(role => role.Antagonist);
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

    private bool HasActiveConversation(NetUserId userId)
    {
        return _bwoinkAdapter.GetRelaySnapshots().Any(snapshot => snapshot.UserId == userId);
    }

    private IList<INetChannel> GetTargetAdmins()
    {
        return _adminManager.ActiveAdmins
            .Where(p => _adminManager.GetAdminData(p)?.HasFlag(AdminFlags.Adminhelp) ?? false)
            .Select(p => p.Channel)
            .ToList();
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (!_seenRelays.ContainsKey(e.Session.UserId))
            return;

        var status = e.NewStatus.ToString();
        if (e.NewStatus == SessionStatus.Disconnected &&
            await _dbManager.GetBanAsync(null, e.Session.UserId, null, null) != null)
        {
            status = "Banned";
        }

        _ = SendAsync(new AHelpApiOutbound.PlayerStatus(
            e.Session.UserId.ToString(),
            e.Session.UserId.ToString(),
            e.Session.Name,
            status,
            DateTimeOffset.UtcNow));
    }

    private async Task SendHelloAsync()
    {
        await SendAsync(new AHelpApiOutbound.Hello(
            ProtocolVersion,
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            _gameTicker.RunLevel.ToString()));
    }

    private async Task SendCurrentConversationsAsync()
    {
        foreach (var snapshot in _bwoinkAdapter.GetRelaySnapshots())
        {
            if (snapshot.RootMessageId == null)
                continue;

            _seenRelays[snapshot.UserId] = new RelaySeenState(
                snapshot.RootMessageId,
                SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Description)));

            await SendConversationUpsertAsync(snapshot);

            if (!string.IsNullOrWhiteSpace(snapshot.Description))
            {
                await SendAsync(new AHelpApiOutbound.AHelpMessage(
                    snapshot.UserId.ToString(),
                    snapshot.UserId.ToString(),
                    "transcript",
                    snapshot.Description,
                    DateTimeOffset.UtcNow));
            }
        }
    }

    private async Task SendConversationUpsertAsync(AHelpRelaySnapshot snapshot)
    {
        _bwoinkAdapter.TryGetAHelpWebhookChannelId(out var webhookChannelId);
        await SendAsync(new AHelpApiOutbound.ConversationUpsert(
            snapshot.UserId.ToString(),
            snapshot.UserId.ToString(),
            snapshot.Username,
            snapshot.CharacterName,
            snapshot.RootMessageId,
            webhookChannelId == 0 ? null : webhookChannelId,
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            snapshot.LastRunLevel.ToString()));
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
        var socket = _socket;
        if (socket is not { State: WebSocketState.Open })
            return;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions);
        await _sendLock.WaitAsync();
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Failed to send Corvax AHelp API message: {e.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
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
        var tcs = new TaskCompletionSource();
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

    private sealed record RelaySeenState(ulong? RootMessageId, byte[] DescriptionHash);
}

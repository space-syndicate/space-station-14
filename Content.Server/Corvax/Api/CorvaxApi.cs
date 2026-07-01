using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Server.Corvax.Api;

public sealed partial class CorvaxApiSystem : EntitySystem
{
    private const string AuthScheme = "CorvaxToken";
    private const string Path = "/corvax/v1/ws";

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, CorvaxApiServiceHandler> _handlers = new();
    private readonly object _handlersLock = new();

    private ISawmill _sawmill = default!;
    private CorvaxApiWebSocketClient _client = default!;
    private string _apiUrl = "ws://127.0.0.1:12120";
    private string _token = string.Empty;

    public bool IsConnected => _client.IsConnected;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("corvax.api");
        _client = new CorvaxApiWebSocketClient(
            _sawmill,
            _jsonOptions,
            "core",
            OnApiConnectedAsync,
            HandleInboundAsync,
            OnApiDisconnected);

        _cfg.OnValueChanged(CCCVars.CorvaxApiUrl, OnApiUrlChanged, true);
        _cfg.OnValueChanged(CCCVars.CorvaxApiToken, OnTokenChanged, true);
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(CCCVars.CorvaxApiUrl, OnApiUrlChanged);
        _cfg.UnsubValueChanged(CCCVars.CorvaxApiToken, OnTokenChanged);
        _client.Stop();
        base.Shutdown();
    }

    public void RegisterService(
        string serviceName,
        Func<Task> onConnected,
        Func<string, Task<bool>> onMessage,
        Action onDisconnected)
    {
        lock (_handlersLock)
        {
            _handlers[serviceName] = new CorvaxApiServiceHandler(onConnected, onMessage, onDisconnected);
        }
    }

    public void UnregisterService(string serviceName)
    {
        lock (_handlersLock)
        {
            _handlers.Remove(serviceName);
        }
    }

    public async Task SendAsync<T>(T payload)
    {
        await _client.SendAsync(payload);
    }

    private void RestartConnection()
    {
        _client.Stop();
        _client.Start(_apiUrl, Path, AuthScheme, _token);
    }

    private void OnApiUrlChanged(string value)
    {
        _apiUrl = value;
        RestartConnection();
    }

    private void OnTokenChanged(string value)
    {
        _token = value;
        RestartConnection();
    }

    private async Task OnApiConnectedAsync()
    {
        foreach (var handler in GetHandlersSnapshot())
        {
            await handler.OnConnected();
        }
    }

    private async Task HandleInboundAsync(string json)
    {
        foreach (var handler in GetHandlersSnapshot())
        {
            if (await handler.OnMessage(json))
                return;
        }

        CorvaxApiInboundBase? message;
        try
        {
            message = JsonSerializer.Deserialize<CorvaxApiInboundBase>(json, _jsonOptions);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Invalid Corvax API message: {e.Message}");
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.Type))
            return;

        await SendAsync(new CorvaxApiResponse(message.RequestId, false, $"Unsupported message type '{message.Type}'"));
    }

    private void OnApiDisconnected()
    {
        foreach (var handler in GetHandlersSnapshot())
        {
            handler.OnDisconnected();
        }
    }

    private CorvaxApiServiceHandler[] GetHandlersSnapshot()
    {
        lock (_handlersLock)
        {
            return _handlers.Values.ToArray();
        }
    }

    private sealed record CorvaxApiServiceHandler(
        Func<Task> OnConnected,
        Func<string, Task<bool>> OnMessage,
        Action OnDisconnected);

    private sealed record CorvaxApiInboundBase(string Type, string? RequestId);

    private sealed record CorvaxApiResponse(string? RequestId, bool Ok, string? Error)
    {
        public string Type { get; init; } = "response";
    }
}

public sealed class CorvaxApiWebSocketClient
{
    private const int MaxInboundMessageBytes = 64 * 1024;
    private const int FailureLogInterval = 12;
    private static readonly TimeSpan MinReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromMinutes(1);

    private readonly ISawmill _sawmill;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _serviceName;
    private readonly Func<Task> _onConnected;
    private readonly Func<string, Task> _onMessage;
    private readonly Action _onDisconnected;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private CancellationTokenSource? _connectCancel;
    private ClientWebSocket? _socket;
    private Uri? _uri;
    private string _token = string.Empty;
    private string _authScheme = string.Empty;

    public CorvaxApiWebSocketClient(
        ISawmill sawmill,
        JsonSerializerOptions jsonOptions,
        string serviceName,
        Func<Task> onConnected,
        Func<string, Task> onMessage,
        Action onDisconnected)
    {
        _sawmill = sawmill;
        _jsonOptions = jsonOptions;
        _serviceName = serviceName;
        _onConnected = onConnected;
        _onMessage = onMessage;
        _onDisconnected = onDisconnected;
    }

    public bool IsConnected => _socket is { State: WebSocketState.Open };

    public void Start(string baseUrl, string path, string authScheme, string token)
    {
        Stop();

        if (!TryBuildUri(baseUrl, path, out var uri))
        {
            _sawmill.Warning($"Corvax API '{_serviceName}' URL is invalid: '{baseUrl}'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _sawmill.Warning($"Corvax API '{_serviceName}' enabled but token is empty; client will not connect.");
            return;
        }

        _token = token;
        _authScheme = authScheme;
        _uri = uri;
        _connectCancel = new CancellationTokenSource();
        _ = Task.Run(() => ConnectLoopAsync(_connectCancel.Token));
    }

    public void Stop()
    {
        _connectCancel?.Cancel();
        _connectCancel = null;
        CloseSocket();
    }

    public async Task SendAsync<T>(T payload)
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
            _sawmill.Warning($"Failed to send Corvax API '{_serviceName}' message: {e.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ConnectLoopAsync(CancellationToken cancel)
    {
        var reconnectDelay = MinReconnectDelay;
        var consecutiveFailures = 0;
        var lastFailureMessage = string.Empty;

        while (!cancel.IsCancellationRequested)
        {
            if (_uri == null)
                return;

            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", $"{_authScheme} {_token}");

            try
            {
                if (consecutiveFailures == 0)
                    _sawmill.Info($"Connecting to Corvax API '{_serviceName}' at {_uri}");

                await socket.ConnectAsync(_uri, cancel);
                _socket = socket;

                if (consecutiveFailures > 0)
                    _sawmill.Info($"Connected to Corvax API '{_serviceName}' after {consecutiveFailures} failed attempt(s).");
                else
                    _sawmill.Info($"Connected to Corvax API '{_serviceName}'.");

                consecutiveFailures = 0;
                lastFailureMessage = string.Empty;
                reconnectDelay = MinReconnectDelay;

                await _onConnected();
                await ReceiveLoopAsync(socket, cancel);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                if (!cancel.IsCancellationRequested)
                {
                    consecutiveFailures++;

                    if (ShouldLogConnectionFailure(consecutiveFailures, e.Message, lastFailureMessage))
                    {
                        _sawmill.Warning(
                            $"Corvax API '{_serviceName}' connection failed: {e.Message}. Retrying in {(int) reconnectDelay.TotalSeconds}s.");
                    }

                    lastFailureMessage = e.Message;
                }
            }
            finally
            {
                if (ReferenceEquals(_socket, socket))
                {
                    _socket = null;
                    _onDisconnected();
                }
            }

            try
            {
                await Task.Delay(reconnectDelay, cancel);
                reconnectDelay = NextReconnectDelay(reconnectDelay);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static bool ShouldLogConnectionFailure(int consecutiveFailures, string message, string lastMessage)
    {
        return consecutiveFailures == 1 ||
               message != lastMessage ||
               consecutiveFailures % FailureLogInterval == 0;
    }

    private static TimeSpan NextReconnectDelay(TimeSpan current)
    {
        var nextSeconds = Math.Min(current.TotalSeconds * 2, MaxReconnectDelay.TotalSeconds);
        return TimeSpan.FromSeconds(nextSeconds);
    }

    private void CloseSocket()
    {
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
    }

    private static bool TryBuildUri(string baseUrl, string path, out Uri uri)
    {
        uri = null!;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            return false;

        if (baseUri.Scheme != Uri.UriSchemeWs && baseUri.Scheme != Uri.UriSchemeWss)
            return false;

        if (!path.StartsWith('/'))
            path = "/" + path;

        var builder = new UriBuilder(baseUri)
        {
            Path = CombinePaths(baseUri.AbsolutePath, path),
            Query = string.Empty,
            Fragment = string.Empty,
        };

        uri = builder.Uri;
        return true;
    }

    private static string CombinePaths(string basePath, string path)
    {
        if (string.IsNullOrWhiteSpace(basePath) || basePath == "/")
            return path;

        return basePath.TrimEnd('/') + "/" + path.TrimStart('/');
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
            await _onMessage(json);
        }
    }
}

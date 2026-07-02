using System.Linq;
using System.Text.Json;
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

    private readonly Dictionary<string, CorvaxApiMessageHandler> _handlers = new();
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

    public void RegisterMessageHandler(
        string handlerName,
        Func<Task> onConnected,
        Func<string, Task<bool>> onMessage,
        Action onDisconnected)
    {
        lock (_handlersLock)
        {
            _handlers[handlerName] = new CorvaxApiMessageHandler(onConnected, onMessage, onDisconnected);
        }
    }

    public void UnregisterMessageHandler(string handlerName)
    {
        lock (_handlersLock)
        {
            _handlers.Remove(handlerName);
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

    private CorvaxApiMessageHandler[] GetHandlersSnapshot()
    {
        lock (_handlersLock)
        {
            return _handlers.Values.ToArray();
        }
    }

    private sealed record CorvaxApiMessageHandler(
        Func<Task> OnConnected,
        Func<string, Task<bool>> OnMessage,
        Action OnDisconnected);

    private sealed record CorvaxApiInboundBase(string Type, string? RequestId);

    private sealed record CorvaxApiResponse(string? RequestId, bool Ok, string? Error)
    {
        public string Type { get; init; } = "response";
    }
}

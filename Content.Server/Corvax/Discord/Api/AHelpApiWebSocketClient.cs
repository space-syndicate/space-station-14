using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Log;

namespace Content.Server.Corvax.Discord;

public sealed class AHelpApiWebSocketClient
{
    private const string AuthScheme = "AHelpToken";
    private const int MaxInboundMessageBytes = 64 * 1024;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ISawmill _sawmill;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Func<Task> _onConnected;
    private readonly Func<string, Task> _onMessage;
    private readonly Action _onDisconnected;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private CancellationTokenSource? _connectCancel;
    private ClientWebSocket? _socket;
    private Uri? _uri;
    private string _token = string.Empty;

    public AHelpApiWebSocketClient(
        ISawmill sawmill,
        JsonSerializerOptions jsonOptions,
        Func<Task> onConnected,
        Func<string, Task> onMessage,
        Action onDisconnected)
    {
        _sawmill = sawmill;
        _jsonOptions = jsonOptions;
        _onConnected = onConnected;
        _onMessage = onMessage;
        _onDisconnected = onDisconnected;
    }

    public bool IsConnected => _socket is { State: WebSocketState.Open };

    public void Start(string host, int port, string path, string token)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(token))
        {
            _sawmill.Warning("Corvax AHelp API enabled but token is empty; client will not connect.");
            return;
        }

        _token = token;
        _uri = new UriBuilder(Uri.UriSchemeWs, host, port, path).Uri;
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
            _sawmill.Warning($"Failed to send Corvax AHelp API message: {e.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ConnectLoopAsync(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            if (_uri == null)
                return;

            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", $"{AuthScheme} {_token}");

            try
            {
                _sawmill.Info($"Connecting to Corvax AHelp bot API at {_uri}");
                await socket.ConnectAsync(_uri, cancel);
                _socket = socket;
                _sawmill.Info("Connected to Corvax AHelp bot API.");
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
                    _sawmill.Warning($"Corvax AHelp bot API connection failed: {e.Message}");
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
                await Task.Delay(ReconnectDelay, cancel);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
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

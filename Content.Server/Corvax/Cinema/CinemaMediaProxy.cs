using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Corvax.Cinema;

/// <summary>Local, bounded media transport. FFmpeg can seek via HTTP ranges without bypassing URL validation.</summary>
internal sealed class CinemaMediaProxy : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop;
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _fetch;
    private readonly List<Task> _requests = new();
    private readonly Task _listening;
    private readonly string _path = "/" + Guid.NewGuid().ToString("N");
    private readonly long _maximum;
    private long _bytes;
    private Exception? _error;
    public string Url { get; }
    public Exception? Error => _error;

    internal CinemaMediaProxy(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fetch,
        long maximum, CancellationToken token)
    {
        _fetch = fetch;
        _maximum = maximum;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        // The listener is private to this preparation job and accepts only an unguessable local URL.
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint) reservation.LocalEndpoint).Port;
        reservation.Stop();
        var prefix = $"http://127.0.0.1:{port}/";
        Url = prefix.TrimEnd('/') + _path;
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _listening = Listen();
    }

    private async Task Listen()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(_stop.Token).ConfigureAwait(false);
                _requests.RemoveAll(task => task.IsCompleted);
                if (_requests.Count >= 4)
                {
                    context.Response.StatusCode = 503;
                    context.Response.Close();
                    continue;
                }
                _requests.Add(Serve(context));
            }
        }
        catch (Exception) when (_stop.IsCancellationRequested) { }
    }

    private async Task Serve(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            if (context.Request.Url?.AbsolutePath != _path || context.Request.HttpMethod is not ("GET" or "HEAD"))
            {
                response.StatusCode = 404;
                return;
            }
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), (Uri?) null);
            if (context.Request.Headers["Range"] is { } range)
                request.Headers.Range = System.Net.Http.Headers.RangeHeaderValue.Parse(range);
            using var upstream = await _fetch(request, _stop.Token).ConfigureAwait(false);
            response.StatusCode = (int) upstream.StatusCode;
            if (!upstream.IsSuccessStatusCode)
            {
                _error = new HttpRequestException($"Cinema media returned HTTP {(int) upstream.StatusCode}");
                return;
            }
            var length = upstream.Content.Headers.ContentLength;
            var total = upstream.Content.Headers.ContentRange?.Length ?? length;
            if (total > _maximum)
                throw new InvalidOperationException("Cinema media exceeds the configured input size limit");
            if (length is { } size)
                response.ContentLength64 = size;
            else
                response.SendChunked = true;
            if (upstream.Content.Headers.ContentRange is { } contentRange)
                response.Headers["Content-Range"] = contentRange.ToString();
            response.Headers["Accept-Ranges"] = string.Join(",", upstream.Headers.AcceptRanges);
            if (context.Request.HttpMethod == "HEAD")
                return;
            await using var input = await upstream.Content.ReadAsStreamAsync(_stop.Token).ConfigureAwait(false);
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = await input.ReadAsync(buffer, _stop.Token).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (Interlocked.Add(ref _bytes, read) > _maximum)
                    throw new InvalidOperationException("Cinema media exceeded the configured transfer limit");
                // FFmpeg closes an earlier request when it seeks; do not treat that as an upstream failure.
                try { await response.OutputStream.WriteAsync(buffer.AsMemory(0, read), _stop.Token).ConfigureAwait(false); }
                catch (Exception e) when (e is IOException or HttpListenerException or ObjectDisposedException) { return; }
            }
        }
        catch (Exception e) when (!_stop.IsCancellationRequested)
        {
            _error = e;
            try { response.StatusCode = 502; } catch (InvalidOperationException) { }
        }
        catch (OperationCanceledException) { }
        finally
        {
            try { response.Close(); } catch (Exception) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try
        {
            await _listening.ConfigureAwait(false);
            await Task.WhenAll(_requests).ConfigureAwait(false);
        }
        finally
        {
            _listener.Close();
            _stop.Dispose();
        }
    }
}

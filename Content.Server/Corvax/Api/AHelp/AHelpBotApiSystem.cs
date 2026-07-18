using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Server.ServerStatus;

namespace Content.Server.Corvax.Api.AHelp;

public sealed partial class AHelpBotApiSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private BwoinkSystem _bwoinkSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private AHelpBotCommandSystem _commands = default!;

    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;
    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;
    private int _apiTimeout;
    private bool _enabled;
    private bool _pushInProgress;
    private bool _pushQueued;
    private readonly object _pushLock = new();

    internal const string CommandPath = "/admin/ahelp/command";
    private const string AHelpTokenScheme = "AHelpToken";

    internal static void RegisterStatusHostHandler(IStatusHost statusHost, IEntitySystemManager entitySystemManager)
    {
        statusHost.AddHandler(context =>
        {
            if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != CommandPath)
                return Task.FromResult(false);

            return entitySystemManager.GetEntitySystem<AHelpBotApiSystem>().HandleCommandApiRequest(context);
        });
    }

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("ahelp.api");
        _cfg.OnValueChanged(CCCVars.AHelpApiTimeout, OnApiTimeoutChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiUrl, OnApiUrlChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiToken, OnApiTokenChanged, true);

        SubscribeLocalEvent<CorvaxAHelpRelayChangedEvent>(OnRelayChanged);
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(CCCVars.AHelpApiEnabled, OnEnabledChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiUrl, OnApiUrlChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiToken, OnApiTokenChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiTimeout, OnApiTimeoutChanged);
        _httpClient.Dispose();
        base.Shutdown();
    }

    private void OnRelayChanged(CorvaxAHelpRelayChangedEvent ev)
    {
        PushCurrentState();
    }

    private AHelpApiEventRequest BuildEventRequest()
    {
        var conversations = _bwoinkSystem.CorvaxGetAHelpRelaySnapshots()
            .Select(snapshot => new AHelpApiConversation(
                snapshot.UserId.ToString(),
                snapshot.UserId.ToString(),
                snapshot.Username,
                snapshot.CharacterName,
                snapshot.RootMessageId,
                snapshot.WebhookChannelId,
                _cfg.GetCVar(CVars.GameHostName),
                _gameTicker.RoundId,
                snapshot.LastRunLevel.ToString(),
                string.IsNullOrWhiteSpace(snapshot.Description) ? null : snapshot.Description))
            .ToArray();

        return new AHelpApiEventRequest(
            "state",
            _cfg.GetCVar(CVars.GameHostName),
            _gameTicker.RoundId,
            _gameTicker.RunLevel.ToString(),
            conversations);
    }

    private void PushCurrentState()
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_apiUrl) || string.IsNullOrWhiteSpace(_apiToken))
            return;

        lock (_pushLock)
        {
            if (_pushInProgress)
            {
                _pushQueued = true;
                return;
            }

            _pushInProgress = true;
        }

        AHelpApiEventRequest request;
        try
        {
            request = BuildEventRequest();
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Unable to build AHelp API event: {e.Message}");
            FinishPush();
            return;
        }

        _ = RunPushAsync(request);
    }

    private async Task RunPushAsync(AHelpApiEventRequest request)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_apiTimeout));
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
            {
                Content = JsonContent.Create(request),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("AHelpToken", _apiToken);

            using var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Warning($"AHelp API event push returned bad status code: {response.StatusCode}");
            }
        }
        catch (TaskCanceledException)
        {
            _sawmill.Warning("AHelp API event push timed out");
        }
        catch (Exception e)
        {
            _sawmill.Warning($"AHelp API event push failed: {e.Message}");
        }
        finally
        {
            FinishPush();
        }
    }

    private void FinishPush()
    {
        var runQueued = false;
        lock (_pushLock)
        {
            _pushInProgress = false;
            if (_pushQueued)
            {
                _pushQueued = false;
                runQueued = true;
            }
        }

        if (runQueued)
            _taskManager.RunOnMainThread(PushCurrentState);
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;
        PushCurrentState();
    }

    private void OnApiUrlChanged(string value)
    {
        _apiUrl = value;
        PushCurrentState();
    }

    private void OnApiTokenChanged(string value)
    {
        _apiToken = value;
        PushCurrentState();
    }

    private void OnApiTimeoutChanged(int value)
    {
        _apiTimeout = Math.Max(1, value);
    }

    private Task<AHelpApiCommandResponse> ExecuteCommandOnMainThread(AHelpApiCommand command, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<AHelpApiCommandResponse>();
        _taskManager.RunOnMainThread(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            completion.TrySetResult(_commands.ExecuteCommand(command));
        });

        return completion.Task;
    }

    internal async Task<bool> HandleCommandApiRequest(IStatusHandlerContext context)
    {
        if (!await CheckCommandApiAccess(context))
            return true;

        if (!_enabled)
        {
            await context.RespondAsync("AHelp API is disabled", HttpStatusCode.ServiceUnavailable);
            return true;
        }

        var command = await ReadCommandApiRequest(context);
        if (command == null)
            return true;

        var response = await ExecuteCommandOnMainThread(command, CancellationToken.None);
        await context.RespondJsonAsync(response);
        return true;
    }

    private async Task<bool> CheckCommandApiAccess(IStatusHandlerContext context)
    {
        if (!context.RequestHeaders.TryGetValue("Authorization", out var authToken))
        {
            await context.RespondAsync("Authorization is required", HttpStatusCode.Unauthorized);
            return false;
        }

        var authHeaderValue = authToken.ToString();
        var spaceIndex = authHeaderValue.IndexOf(' ');
        if (spaceIndex == -1)
        {
            await context.RespondAsync("Invalid Authorization header value", HttpStatusCode.BadRequest);
            return false;
        }

        var authScheme = authHeaderValue[..spaceIndex];
        var authValue = authHeaderValue[spaceIndex..].Trim();

        if (authScheme != AHelpTokenScheme)
        {
            await context.RespondAsync("Invalid Authorization scheme", HttpStatusCode.BadRequest);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_apiToken) &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(authValue),
                Encoding.UTF8.GetBytes(_apiToken)))
        {
            return true;
        }

        await context.RespondAsync("Authorization is invalid", HttpStatusCode.Unauthorized);
        _sawmill.Info($"Unauthorized access attempt to AHelp command API from {context.RemoteEndPoint}");
        return false;
    }

    private static async Task<AHelpApiCommand?> ReadCommandApiRequest(IStatusHandlerContext context)
    {
        try
        {
            var command = await context.RequestBodyJsonAsync<AHelpApiCommand>();
            if (command == null)
                await context.RespondAsync("Request body is null", HttpStatusCode.BadRequest);

            return command;
        }
        catch (Exception e)
        {
            await context.RespondAsync($"Unable to parse request body: {e.Message}", HttpStatusCode.BadRequest);
            return null;
        }
    }

}

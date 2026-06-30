using System.Text.Json;
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
using Robust.Shared.Network;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpExternalApiSystem : SharedBwoinkSystem
{
    private const string ProtocolVersion = "1";
    private const string Path = "/ahelp/v1/ws";

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

    private readonly Dictionary<NetUserId, RelaySeenState> _seenRelays = new();

    private ISawmill _sawmill = default!;
    private AHelpBwoinkReflectionAdapter _bwoinkAdapter = default!;
    private AHelpDiscordRelayService _relayService = default!;
    private AHelpApiWebSocketClient _apiClient = default!;
    private bool _enabled;
    private string _host = "127.0.0.1";
    private int _port = 12120;
    private string _token = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("corvax.ahelp.api");
        _bwoinkAdapter = new AHelpBwoinkReflectionAdapter(_bwoinkSystem);
        _relayService = new AHelpDiscordRelayService(
            _adminManager,
            _playerManager,
            _gameTicker,
            _bwoinkAdapter,
            RaiseNetworkEvent);
        _apiClient = new AHelpApiWebSocketClient(
            _sawmill,
            _jsonOptions,
            OnApiConnectedAsync,
            HandleInboundAsync,
            OnApiDisconnected);

        _cfg.OnValueChanged(CCCVars.AHelpApiEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiHost, OnHostChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiPort, OnPortChanged, true);
        _cfg.OnValueChanged(CCCVars.AHelpApiToken, OnTokenChanged, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(CCCVars.AHelpApiEnabled, OnEnabledChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiHost, OnHostChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiPort, OnPortChanged);
        _cfg.UnsubValueChanged(CCCVars.AHelpApiToken, OnTokenChanged);
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _apiClient.Stop();
        base.Shutdown();
    }

    private void RestartConnection()
    {
        _apiClient.Stop();

        if (_enabled)
            _apiClient.Start(_host, _port, Path, _token);
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;
        RestartConnection();
    }

    private void OnHostChanged(string value)
    {
        _host = value;
        RestartConnection();
    }

    private void OnPortChanged(int value)
    {
        _port = value;
        RestartConnection();
    }

    private void OnTokenChanged(string value)
    {
        _token = value;
        RestartConnection();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _seenRelays.Clear();
        _ = SendAsync(new AHelpApiOutbound.RoundChanged(_gameTicker.RoundId, _gameTicker.RunLevel.ToString()));
    }
}

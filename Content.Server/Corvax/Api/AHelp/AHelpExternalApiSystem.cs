using System.Text.Json;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Corvax.Api;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Api.AHelp;

public sealed partial class AHelpExternalApiSystem : SharedBwoinkSystem
{
    private const string ProtocolVersion = "1";
    private const string ServiceName = "ahelp";

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private CorvaxApiSystem _corvaxApi = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IServerDbManager _dbManager = default!;
    [Dependency] private BwoinkSystem _bwoinkSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<NetUserId, RelaySeenState> _seenRelays = new();

    private ISawmill _sawmill = default!;
    private AHelpBwoinkReflectionAdapter _bwoinkAdapter = default!;
    private AHelpExternalRelayService _relayService = default!;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("corvax.ahelp.api");
        _bwoinkAdapter = new AHelpBwoinkReflectionAdapter(_bwoinkSystem);
        _relayService = new AHelpExternalRelayService(
            _adminManager,
            _playerManager,
            _gameTicker,
            _bwoinkAdapter,
            RaiseNetworkEvent);
        _corvaxApi.RegisterService(
            ServiceName,
            OnApiConnectedAsync,
            HandleInboundAsync,
            OnApiDisconnected);

        _cfg.OnValueChanged(CCCVars.AHelpEnabled, OnEnabledChanged, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(CCCVars.AHelpEnabled, OnEnabledChanged);
        _corvaxApi.UnregisterService(ServiceName);
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        base.Shutdown();
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;

        if (_enabled && _corvaxApi.IsConnected)
            _ = OnApiConnectedAsync();
        else if (!_enabled)
            _seenRelays.Clear();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _seenRelays.Clear();

        if (_enabled)
            _ = SendAsync(new AHelpApiOutbound.RoundChanged(_gameTicker.RoundId, _gameTicker.RunLevel.ToString()));
    }
}

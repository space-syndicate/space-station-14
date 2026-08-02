using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.MapRotation;

/// <summary>
/// Keeps the map-rotation cache used during a vote, while the authoritative data lives in the AHelp bot.
/// </summary>
public sealed partial class CorvaxMapRotationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly HttpClient _httpClient = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private ISawmill _log = default!;
    private RotationServerStats? _stats;
    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;
    private int _rareMapInterval = 5;
    private int _apiTimeout = 5;
    private bool _enabled;
    private bool _ahelpApiEnabled;
    private bool _nextMapForcedByAdmin;
    private string? _firstLoadedRoundMap;
    private int? _lastRecordedRound;

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("corvax.map_rotation");

        SubscribeLocalEvent<PostGameMapLoad>(ev => _firstLoadedRoundMap ??= ev.GameMap.ID);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _firstLoadedRoundMap = null);
        Subs.CVar(_cfg, CCCVars.MapRotationEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CCCVars.MapRotationRareMapInterval, value => _rareMapInterval = value, true);
        Subs.CVar(_cfg, CCCVars.AHelpApiEnabled, value => _ahelpApiEnabled = value, true);
        Subs.CVar(_cfg, CCCVars.AHelpApiUrl, OnAHelpApiUrlChanged, true);
        Subs.CVar(_cfg, CCCVars.AHelpApiToken, value => _apiToken = value.Trim(), true);
        Subs.CVar(_cfg, CCCVars.AHelpApiTimeout, value => _apiTimeout = value, true);
        Subs.CVar(_cfg, CCVars.GameMap, OnGameMapCVarChanged, true);
        RefreshStats();
    }

    public override void Shutdown()
    {
        _httpClient.Dispose();
        base.Shutdown();
    }

    public void MarkNextMapForcedByAdmin(string mapId)
    {
        _nextMapForcedByAdmin = !string.IsNullOrWhiteSpace(mapId);
        if (_nextMapForcedByAdmin)
            _log.Info($"Next round map selection marked as forced by admin: {mapId}");
    }

    public bool TryGetRareMap(
        IReadOnlyCollection<GameMapPrototype> eligibleMaps,
        GameMapPrototype voteWinner,
        out GameMapPrototype map)
    {
        map = default!;
        if (!IsConfigured() || _stats == null || !IsRareRotationRound(_stats.RotationRound, _rareMapInterval))
            return false;

        map = eligibleMaps
            .Select(proto => (Proto: proto, Stats: _stats.Maps.GetValueOrDefault(proto.ID)))
            .OrderBy(item => item.Stats?.LastStartedAt.HasValue ?? false)
            .ThenBy(item => item.Stats?.LastStartedAt ?? DateTime.MinValue)
            .ThenBy(item => item.Stats?.StartCount ?? 0)
            // When maps are equally rare, the vote result is the fair tie-breaker.
            .ThenBy(item => item.Proto.ID != voteWinner.ID)
            .ThenBy(item => item.Proto.ID)
            .First().Proto;
        return true;
    }

    public void RecordMapVoteResult(
        IReadOnlyCollection<GameMapPrototype> eligibleMaps,
        IReadOnlyDictionary<object, int> votesPerOption,
        GameMapPrototype voteWinner,
        GameMapPrototype finalSelectedMap,
        bool rareRotationApplied)
    {
        if (!IsConfigured())
            return;

        PostVoteResult(new
        {
            eligibleMaps = eligibleMaps.Select(map => map.ID).ToArray(),
            votes = eligibleMaps.ToDictionary(map => map.ID, map => votesPerOption.GetValueOrDefault(map)),
            voteWinner = voteWinner.ID,
            finalSelectedMap = finalSelectedMap.ID,
            rareRotationApplied,
        });
    }

    internal static bool IsRareRotationRound(int completedRotationRounds, int rareMapInterval)
    {
        return rareMapInterval > 0 && (completedRotationRounds + 1) % rareMapInterval == 0;
    }

    private void OnGameMapCVarChanged(string mapId)
    {
        // The initial CVar value is not an administrator selection.
        if (_lastRecordedRound != null)
            MarkNextMapForcedByAdmin(mapId);
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!IsConfigured() || _lastRecordedRound == ev.RoundId)
            return;

        _lastRecordedRound = ev.RoundId;
        PostRoundStart(new
        {
            mapId = _firstLoadedRoundMap,
            forcedByAdmin = _nextMapForcedByAdmin,
        });
        _firstLoadedRoundMap = null;
        _nextMapForcedByAdmin = false;
    }

    private bool IsConfigured()
    {
        if (!_enabled || !_ahelpApiEnabled)
            return false;
        if (_rareMapInterval <= 0 || _apiTimeout <= 0 || string.IsNullOrWhiteSpace(_apiUrl) ||
            string.IsNullOrWhiteSpace(_apiToken))
        {
            return false;
        }
        return true;
    }

    private void OnAHelpApiUrlChanged(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint))
        {
            _apiUrl = string.Empty;
            return;
        }

        _apiUrl = endpoint.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private async void RefreshStats()
    {
        if (!IsConfigured())
            return;
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "/map-rotation/v1/state");
            using var response = await _httpClient.SendAsync(request, CreateCancellationToken());
            response.EnsureSuccessStatusCode();
            _stats = await response.Content.ReadFromJsonAsync<RotationServerStats>(_jsonOptions);
        }
        catch (Exception e)
        {
            _log.Warning($"Could not load map rotation state from AHelp bot: {e.Message}");
        }
    }

    private async void PostRoundStart(object payload)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "/map-rotation/v1/round-start", payload);
            using var response = await _httpClient.SendAsync(request, CreateCancellationToken());
            response.EnsureSuccessStatusCode();
            _stats = await response.Content.ReadFromJsonAsync<RotationServerStats>(_jsonOptions);
        }
        catch (Exception e)
        {
            _log.Warning($"Could not store map rotation round start in AHelp bot: {e.Message}");
        }
    }

    private async void PostVoteResult(object payload)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "/map-rotation/v1/vote-result", payload);
            using var response = await _httpClient.SendAsync(request, CreateCancellationToken());
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            _log.Warning($"Could not store map vote result in AHelp bot: {e.Message}");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? payload = null)
    {
        var request = new HttpRequestMessage(method, $"{_apiUrl}{path}");
        request.Headers.TryAddWithoutValidation("Authorization", $"AHelpToken {_apiToken}");
        if (payload != null)
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return request;
    }

    private CancellationToken CreateCancellationToken()
    {
        return new CancellationTokenSource(TimeSpan.FromSeconds(_apiTimeout)).Token;
    }

    private sealed class RotationServerStats
    {
        public int RotationRound { get; set; }
        public Dictionary<string, MapRotationMapStats> Maps { get; set; } = new();
    }

    private sealed class MapRotationMapStats
    {
        public DateTime? LastStartedAt { get; set; }
        public int StartCount { get; set; }
    }
}

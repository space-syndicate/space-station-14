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
    private int _roundInterval = 5;
    private MapRotationStrategy _strategy = MapRotationStrategy.PeriodicRare;
    private int _apiTimeout = 5;
    private bool _enabled;
    private bool _ahelpApiEnabled;
    private bool _nextMapForcedByAdmin;
    private string? _firstLoadedRoundMap;
    private int? _lastRecordedRound;

    public bool UsesPeriodicRareStrategy => _strategy == MapRotationStrategy.PeriodicRare;

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("corvax.map_rotation");

        SubscribeLocalEvent<PostGameMapLoad>(ev => _firstLoadedRoundMap ??= ev.GameMap.ID);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _firstLoadedRoundMap = null);
        Subs.CVar(_cfg, CCCVars.MapRotationEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CCCVars.MapRotationStrategy, OnStrategyChanged, true);
        Subs.CVar(_cfg, CCCVars.MapRotationRoundInterval, value => _roundInterval = value, true);
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
        if (!IsConfigured() || _strategy != MapRotationStrategy.PeriodicRare || _stats == null ||
            !IsRareRotationRound(_stats.RotationRound, _roundInterval))
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

    public IEnumerable<GameMapPrototype> FilterVoteMaps(IEnumerable<GameMapPrototype> maps)
    {
        var candidates = maps.ToArray();
        if (!IsConfigured() || _strategy != MapRotationStrategy.RecentExclusion || _stats == null)
            return candidates;

        var allowed = candidates
            .Where(map => !IsOnCooldown(_stats.RotationRound, _stats.Maps.GetValueOrDefault(map.ID)?.LastStartedRound,
                _roundInterval))
            .ToArray();
        if (allowed.Length > 0)
            return allowed;

        // A small pool must never produce an empty vote. If every map is on
        // cooldown, expose the one that was started least recently.
        return candidates
            .OrderBy(map => _stats.Maps.GetValueOrDefault(map.ID)?.LastStartedRound ?? int.MinValue)
            .ThenBy(map => map.ID)
            .Take(1)
            .ToArray();
    }

    public int GetDisplayedVoteCount(string mapId, int currentVotes)
    {
        if (!IsConfigured() || _strategy != MapRotationStrategy.CumulativeVotes || _stats == null)
            return currentVotes;

        return GetVoteScore(mapId, currentVotes, _stats.CumulativeVotes, true);
    }

    public bool TryGetRotationMap(
        IReadOnlyCollection<GameMapPrototype> eligibleMaps,
        IReadOnlyDictionary<object, int> votesPerOption,
        GameMapPrototype voteWinner,
        out GameMapPrototype map)
    {
        map = default!;
        if (!IsConfigured() || _stats == null)
            return false;

        if (_strategy == MapRotationStrategy.PeriodicRare)
            return TryGetRareMap(eligibleMaps, voteWinner, out map);

        IEnumerable<GameMapPrototype> candidates = eligibleMaps;
        if (_strategy == MapRotationStrategy.RecentExclusion)
            candidates = FilterVoteMaps(candidates);

        map = candidates
            .Select(proto => (
                Proto: proto,
                Votes: GetVoteScore(
                    proto.ID,
                    votesPerOption.GetValueOrDefault(proto),
                    _stats.CumulativeVotes,
                    _strategy == MapRotationStrategy.CumulativeVotes)))
            .OrderByDescending(item => item.Votes)
            .ThenBy(item => item.Proto.ID != voteWinner.ID)
            .ThenBy(item => item.Proto.ID)
            .Select(item => item.Proto)
            .FirstOrDefault()!;

        return map != null;
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
            accumulateVotes = _strategy == MapRotationStrategy.CumulativeVotes,
        });
    }

    internal static bool IsRareRotationRound(int completedRotationRounds, int rareMapInterval)
    {
        return rareMapInterval > 0 && (completedRotationRounds + 1) % rareMapInterval == 0;
    }

    internal static bool IsOnCooldown(int completedRotationRounds, int? lastStartedRound, int roundInterval)
    {
        return lastStartedRound != null && roundInterval > 0 &&
            completedRotationRounds - lastStartedRound.Value < roundInterval;
    }

    internal static int GetVoteScore(
        string mapId,
        int currentVotes,
        IReadOnlyDictionary<string, int> cumulativeVotes,
        bool accumulateVotes)
    {
        return currentVotes + (accumulateVotes ? cumulativeVotes.GetValueOrDefault(mapId) : 0);
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
        if ((_strategy != MapRotationStrategy.CumulativeVotes && _roundInterval <= 0) ||
            _apiTimeout <= 0 || string.IsNullOrWhiteSpace(_apiUrl) ||
            string.IsNullOrWhiteSpace(_apiToken))
        {
            return false;
        }
        return true;
    }

    private void OnStrategyChanged(string value)
    {
        _strategy = value.Trim().ToLowerInvariant() switch
        {
            "periodic_rare" => MapRotationStrategy.PeriodicRare,
            "recent_exclusion" => MapRotationStrategy.RecentExclusion,
            "cumulative_votes" => MapRotationStrategy.CumulativeVotes,
            _ => MapRotationStrategy.Invalid,
        };

        if (_strategy == MapRotationStrategy.Invalid)
            _log.Error($"Unknown Corvax map rotation strategy: {value}");
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
            _stats = null;
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
            _stats = null;
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
            _stats = await response.Content.ReadFromJsonAsync<RotationServerStats>(_jsonOptions);
        }
        catch (Exception e)
        {
            _stats = null;
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
        public Dictionary<string, int> CumulativeVotes { get; set; } = new();
    }

    private sealed class MapRotationMapStats
    {
        public DateTime? LastStartedAt { get; set; }
        public int? LastStartedRound { get; set; }
        public int StartCount { get; set; }
    }

    private enum MapRotationStrategy
    {
        Invalid,
        PeriodicRare,
        RecentExclusion,
        CumulativeVotes,
    }
}

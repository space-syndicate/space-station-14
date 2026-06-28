using System.Linq;
using System.IO;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.MapRotation;

public sealed partial class CorvaxMapRotationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IResourceManager _resMan = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    private static readonly ResPath StatsPath = new("/corvax_map_rotation.yml");

    private ISawmill _log = default!;
    private RotationStatsFile _stats = new();
    private string _serverKey = string.Empty;
    private bool _enabled;
    private int _rareMapInterval = 5;
    private bool _configValid;
    private bool _loaded;
    private bool _dirty;
    private bool _enabledInitialized;
    private bool _serverKeyInitialized;
    private bool _rareMapIntervalInitialized;
    private bool _gameMapCVarInitialized;
    private bool _nextMapForcedByAdmin;
    private string? _firstLoadedRoundMap;
    private int? _lastRecordedRound;

    public override void Initialize()
    {
        base.Initialize();

        _log = Logger.GetSawmill("corvax.map_rotation");

        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetLoadedRoundMap());

        Subs.CVar(_cfg, CCCVars.MapRotationEnabled, value =>
        {
            _enabled = value;
            _enabledInitialized = true;
            ValidateConfig();
        }, true);
        Subs.CVar(_cfg, CCCVars.MapRotationServerKey, value =>
        {
            _serverKey = value.Trim();
            _serverKeyInitialized = true;
            ValidateConfig();
        }, true);
        Subs.CVar(_cfg, CCCVars.MapRotationRareMapInterval, value =>
        {
            _rareMapInterval = value;
            _rareMapIntervalInitialized = true;
            ValidateConfig();
        }, true);
        Subs.CVar(_cfg, CCVars.GameMap, OnGameMapCVarChanged, true);

        LoadStats();
        SyncCurrentMapPool();
        SaveIfDirty();
    }

    public void MarkNextMapForcedByAdmin(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            _nextMapForcedByAdmin = false;
            return;
        }

        _nextMapForcedByAdmin = true;
        _log.Info($"Next round map selection marked as forced by admin: {mapId}");
    }

    private void OnGameMapCVarChanged(string mapId)
    {
        if (!_gameMapCVarInitialized)
        {
            _gameMapCVarInitialized = true;
            return;
        }

        MarkNextMapForcedByAdmin(mapId);
    }

    public bool TryGetRareMap(IReadOnlyCollection<GameMapPrototype> eligibleMaps, out GameMapPrototype map)
    {
        map = default!;

        if (!_enabled || !_configValid || eligibleMaps.Count == 0)
            return false;

        EnsureReady();

        var server = GetOrCreateServer();
        if (!IsRareRotationRound(server.RotationRound, _rareMapInterval))
            return false;

        if (!TryGetConfiguredMapPoolIds(out var poolIds))
            return false;

        var candidates = eligibleMaps
            .Where(x => poolIds.Contains(x.ID))
            .Select(x => (Proto: x, Stats: server.Maps.GetValueOrDefault(x.ID)))
            .Where(x => x.Stats != null)
            // Never-started maps have maximum priority. Fully equal candidates use
            // prototype ID ordering so the choice is stable across restarts.
            .OrderBy(x => x.Stats!.LastStartedAt.HasValue)
            .ThenBy(x => x.Stats!.LastStartedAt ?? DateTime.MinValue)
            .ThenBy(x => x.Stats!.StartCount)
            .ThenBy(x => x.Proto.ID)
            .ToArray();

        if (candidates.Length == 0)
            return false;

        map = candidates[0].Proto;
        return true;
    }

    public void ResetPendingRoundState()
    {
        _firstLoadedRoundMap = null;
        _nextMapForcedByAdmin = false;
    }

    internal static bool IsRareRotationRound(int completedRotationRounds, int rareMapInterval)
    {
        return rareMapInterval > 0 && (completedRotationRounds + 1) % rareMapInterval == 0;
    }

    private void ResetLoadedRoundMap()
    {
        _firstLoadedRoundMap = null;
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        _firstLoadedRoundMap ??= ev.GameMap.ID;
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_enabled || !_configValid)
        {
            ResetPendingRoundState();
            return;
        }

        if (_lastRecordedRound == ev.RoundId)
            return;

        EnsureReady();

        var server = GetOrCreateServer();
        server.RotationRound++;

        if (_firstLoadedRoundMap == null)
        {
            _log.Warning($"Round {ev.RoundId} started, but no loaded round map was recorded.");
        }
        else if (_nextMapForcedByAdmin)
        {
            _log.Info($"Round {ev.RoundId} map {_firstLoadedRoundMap} was forced by admin; map rarity statistics were not updated.");
        }
        else
        {
            var mapStats = GetOrCreateMapStats(server, _firstLoadedRoundMap);
            mapStats.LastStartedAt = DateTime.UtcNow;
            mapStats.StartCount++;
        }

        _lastRecordedRound = ev.RoundId;
        _dirty = true;
        SaveIfDirty();
        ResetPendingRoundState();
    }

    private void EnsureReady()
    {
        if (!_loaded)
            LoadStats();

        SyncCurrentMapPool();
        SaveIfDirty();
    }

    private void ValidateConfig()
    {
        _configValid = false;

        if (!_enabledInitialized || !_serverKeyInitialized || !_rareMapIntervalInitialized)
            return;

        if (!_enabled)
            return;

        if (_rareMapInterval <= 0)
        {
            _log.Error($"{CCCVars.MapRotationRareMapInterval.Name} must be greater than zero. Corvax map rotation priority is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_serverKey) ||
            _serverKey.Any(x => !char.IsLetterOrDigit(x) && x != '_' && x != '-'))
        {
            _log.Error($"{CCCVars.MapRotationServerKey.Name} must be a non-empty key containing only letters, digits, '_' or '-'. Corvax map rotation priority is disabled.");
            return;
        }

        _configValid = true;
    }

    private void SyncCurrentMapPool()
    {
        if (!_enabled || !_configValid)
            return;

        if (!TryGetConfiguredMapPoolIds(out var currentMapIds))
            return;

        var server = GetOrCreateServer();

        foreach (var mapId in server.Maps.Keys.ToArray())
        {
            if (currentMapIds.Contains(mapId))
                continue;

            server.Maps.Remove(mapId);
            _dirty = true;
        }

        foreach (var mapId in currentMapIds)
        {
            if (server.Maps.ContainsKey(mapId))
                continue;

            server.Maps[mapId] = new MapRotationMapStats();
            _dirty = true;
        }
    }

    private bool TryGetConfiguredMapPoolIds(out HashSet<string> mapIds)
    {
        mapIds = new HashSet<string>();
        var poolPrototype = _cfg.GetCVar(CCVars.GameMapPool);

        if (!_prototypeManager.TryIndex<GameMapPoolPrototype>(poolPrototype, out var pool))
        {
            _log.Error($"Could not index configured map pool prototype {poolPrototype}; Corvax map rotation statistics were not synchronized.");
            return false;
        }

        foreach (var mapId in pool.Maps)
            mapIds.Add(mapId);

        return true;
    }

    private RotationServerStats GetOrCreateServer()
    {
        if (!_stats.Servers.TryGetValue(_serverKey, out var server))
        {
            server = new RotationServerStats();
            _stats.Servers[_serverKey] = server;
            _dirty = true;
        }

        return server;
    }

    private MapRotationMapStats GetOrCreateMapStats(RotationServerStats server, string mapId)
    {
        if (!server.Maps.TryGetValue(mapId, out var stats))
        {
            stats = new MapRotationMapStats();
            server.Maps[mapId] = stats;
        }

        return stats;
    }

    private void LoadStats()
    {
        _loaded = true;

        if (!_resMan.UserData.Exists(StatsPath))
        {
            _stats = new RotationStatsFile();
            _dirty = true;
            return;
        }

        try
        {
            using var reader = _resMan.UserData.OpenText(StatsPath);
            var document = DataNodeParser.ParseYamlStream(reader).FirstOrDefault();
            _stats = document == null
                ? new RotationStatsFile()
                : _serialization.Read<RotationStatsFile>(document.Root, notNullableOverride: true);

            if (_stats.Version <= 0)
            {
                _log.Error($"Invalid version in {StatsPath}; starting with empty Corvax map rotation statistics.");
                BackupCorruptStats();
                _stats = new RotationStatsFile();
                _dirty = true;
            }
        }
        catch (Exception e)
        {
            _log.Error($"Failed to read {StatsPath}: {e}");
            BackupCorruptStats();
            _stats = new RotationStatsFile();
            _dirty = true;
        }
    }

    private void BackupCorruptStats()
    {
        if (!_resMan.UserData.Exists(StatsPath))
            return;

        var backup = new ResPath($"/corvax_map_rotation.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.yml");
        try
        {
            _resMan.UserData.Rename(StatsPath, backup);
            _log.Warning($"Moved corrupt Corvax map rotation statistics to {backup}.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to back up corrupt Corvax map rotation statistics: {e}");
        }
    }

    private void SaveIfDirty()
    {
        if (!_dirty)
            return;

        var tempPath = new ResPath($"{StatsPath}.tmp");

        try
        {
            using (var writer = _resMan.UserData.OpenWriteText(tempPath))
            {
                _serialization.WriteValue(_stats, alwaysWrite: true, notNullableOverride: true).Write(writer);
            }

            if (_resMan.UserData.RootDir is { } rootDir)
            {
                var tempFullPath = Path.Combine(rootDir, tempPath.ToRelativeSystemPath());
                var targetFullPath = Path.Combine(rootDir, StatsPath.ToRelativeSystemPath());
                File.Move(tempFullPath, targetFullPath, true);
            }
            else
            {
                if (_resMan.UserData.Exists(StatsPath))
                    _resMan.UserData.Delete(StatsPath);

                _resMan.UserData.Rename(tempPath, StatsPath);
            }

            _dirty = false;
        }
        catch (Exception e)
        {
            _log.Error($"Failed to save {StatsPath}: {e}");

            if (_resMan.UserData.Exists(tempPath))
                _resMan.UserData.Delete(tempPath);
        }
    }

    [DataDefinition]
    private sealed partial class RotationStatsFile
    {
        [DataField("version")]
        public int Version = 1;

        [DataField("servers")]
        public Dictionary<string, RotationServerStats> Servers = new();
    }

    [DataDefinition]
    private sealed partial class RotationServerStats
    {
        [DataField("rotationRound")]
        public int RotationRound;

        [DataField("maps")]
        public Dictionary<string, MapRotationMapStats> Maps = new();
    }

    [DataDefinition]
    private sealed partial class MapRotationMapStats
    {
        [DataField("lastStartedAt")]
        public DateTime? LastStartedAt;

        [DataField("startCount")]
        public int StartCount;
    }

}

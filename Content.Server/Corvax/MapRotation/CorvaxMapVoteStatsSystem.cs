using System.IO;
using System.Linq;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.MapRotation;

public sealed partial class CorvaxMapVoteStatsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resMan = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    private static readonly ResPath VoteResultsPath = new("/corvax_map_vote_results.yml");

    private ISawmill _log = default!;
    private string _serverKey = string.Empty;
    private bool _enabled;
    private bool _configValid;
    private bool _enabledInitialized;
    private bool _serverKeyInitialized;

    public override void Initialize()
    {
        base.Initialize();

        _log = Logger.GetSawmill("corvax.map_vote_stats");

        Subs.CVar(_cfg, CCCVars.MapRotationServerKey, value =>
        {
            _serverKey = value.Trim();
            _serverKeyInitialized = true;
            ValidateConfig();
        }, true);

        Subs.CVar(_cfg, CCCVars.MapRotationEnabled, value =>
        {
            _enabled = value;
            _enabledInitialized = true;
            ValidateConfig();
        }, true);
    }

    public void RecordMapVoteResult(
        IReadOnlyCollection<GameMapPrototype> eligibleMaps,
        IReadOnlyDictionary<object, int> votesPerOption,
        GameMapPrototype voteWinner,
        GameMapPrototype finalSelectedMap,
        bool rareRotationApplied)
    {
        if (!_enabled || !_configValid)
            return;

        var now = DateTime.UtcNow;
        var results = LoadVoteResults();
        var serverResults = GetOrCreateVoteResultsServer(results);
        var month = $"{now:yyyy-MM}";

        if (!serverResults.Months.TryGetValue(month, out var monthResults))
        {
            monthResults = new List<MapVoteGroupStats>();
            serverResults.Months[month] = monthResults;
        }

        AddVoteToMonthGroups(
            monthResults,
            now,
            eligibleMaps.Select(map => map.ID),
            eligibleMaps.ToDictionary(
                map => map.ID,
                map => votesPerOption.TryGetValue(map, out var votes) ? votes : 0),
            voteWinner.ID,
            finalSelectedMap.ID,
            rareRotationApplied);

        SaveVoteResults(results);
    }

    private void ValidateConfig()
    {
        _configValid = false;

        if (!_enabledInitialized || !_serverKeyInitialized)
            return;

        if (!_enabled)
            return;

        if (string.IsNullOrWhiteSpace(_serverKey) ||
            _serverKey.Any(x => !char.IsLetterOrDigit(x) && x != '_' && x != '-'))
        {
            _log.Warning($"{CCCVars.MapRotationServerKey.Name} must be a non-empty key containing only letters, digits, '_' or '-'. Corvax map vote statistics are disabled.");
            return;
        }

        _configValid = true;
    }

    private MapVoteResultsFile LoadVoteResults()
    {
        if (!_resMan.UserData.Exists(VoteResultsPath))
            return new MapVoteResultsFile();

        try
        {
            using var reader = _resMan.UserData.OpenText(VoteResultsPath);
            var document = DataNodeParser.ParseYamlStream(reader).FirstOrDefault();
            var results = document == null
                ? new MapVoteResultsFile()
                : _serialization.Read<MapVoteResultsFile>(document.Root, notNullableOverride: true);

            if (results.Version <= 0)
            {
                _log.Error($"Invalid version in {VoteResultsPath}; starting with empty Corvax map vote results.");
                BackupCorruptVoteResults();
                return new MapVoteResultsFile();
            }

            return results;
        }
        catch (Exception e)
        {
            _log.Error($"Failed to read {VoteResultsPath}: {e}");
            BackupCorruptVoteResults();
            return new MapVoteResultsFile();
        }
    }

    internal static void AddVoteToMonthGroups(
        List<MapVoteGroupStats> groups,
        DateTime startedAt,
        IEnumerable<string> eligibleMaps,
        IReadOnlyDictionary<string, int> votes,
        string winner,
        string finalSelectedMap,
        bool rareRotationApplied)
    {
        var group = GetOrCreateVoteGroup(groups, eligibleMaps);
        group.VoteCount++;
        group.FirstVoteAt = MinDate(group.FirstVoteAt, startedAt);
        group.LastVoteAt = MaxDate(group.LastVoteAt, startedAt);
        AddCounts(group.Votes, votes);

        if (!string.IsNullOrEmpty(winner))
            group.Winners[winner] = group.Winners.GetValueOrDefault(winner) + 1;

        if (!string.IsNullOrEmpty(finalSelectedMap))
            group.FinalSelectedMaps[finalSelectedMap] = group.FinalSelectedMaps.GetValueOrDefault(finalSelectedMap) + 1;

        if (rareRotationApplied)
            group.RareRotationAppliedCount++;
    }

    private static MapVoteGroupStats GetOrCreateVoteGroup(List<MapVoteGroupStats> groups, IEnumerable<string> eligibleMaps)
    {
        var normalizedMaps = eligibleMaps
            .Distinct()
            .OrderBy(mapId => mapId)
            .ToList();

        foreach (var group in groups)
        {
            if (group.EligibleMaps.SequenceEqual(normalizedMaps))
                return group;
        }

        var newGroup = new MapVoteGroupStats { EligibleMaps = normalizedMaps };
        groups.Add(newGroup);
        groups.Sort((left, right) => string.Join('\n', left.EligibleMaps).CompareTo(string.Join('\n', right.EligibleMaps)));
        return newGroup;
    }

    private static void AddCounts(Dictionary<string, int> target, IReadOnlyDictionary<string, int> source)
    {
        foreach (var (key, value) in source)
            target[key] = target.GetValueOrDefault(key) + value;
    }

    private static DateTime? MinDate(DateTime? left, DateTime? right)
    {
        return left == null || right < left ? right : left;
    }

    private static DateTime? MaxDate(DateTime? left, DateTime? right)
    {
        return left == null || right > left ? right : left;
    }

    private void BackupCorruptVoteResults()
    {
        if (!_resMan.UserData.Exists(VoteResultsPath))
            return;

        var backup = new ResPath($"/corvax_map_vote_results.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.yml");
        try
        {
            _resMan.UserData.Rename(VoteResultsPath, backup);
            _log.Warning($"Moved corrupt Corvax map vote results to {backup}.");
        }
        catch (Exception e)
        {
            _log.Error($"Failed to back up corrupt Corvax map vote results: {e}");
        }
    }

    private void SaveVoteResults(MapVoteResultsFile results)
    {
        var tempPath = new ResPath($"{VoteResultsPath}.tmp");

        try
        {
            using (var writer = _resMan.UserData.OpenWriteText(tempPath))
            {
                _serialization.WriteValue(results, alwaysWrite: true, notNullableOverride: true).Write(writer);
            }

            if (_resMan.UserData.RootDir is { } rootDir)
            {
                var tempFullPath = Path.Combine(rootDir, tempPath.ToRelativeSystemPath());
                var targetFullPath = Path.Combine(rootDir, VoteResultsPath.ToRelativeSystemPath());
                File.Move(tempFullPath, targetFullPath, true);
            }
            else
            {
                if (_resMan.UserData.Exists(VoteResultsPath))
                    _resMan.UserData.Delete(VoteResultsPath);

                _resMan.UserData.Rename(tempPath, VoteResultsPath);
            }
        }
        catch (Exception e)
        {
            _log.Error($"Failed to save {VoteResultsPath}: {e}");

            if (_resMan.UserData.Exists(tempPath))
                _resMan.UserData.Delete(tempPath);
        }
    }

    private VoteResultsServer GetOrCreateVoteResultsServer(MapVoteResultsFile results)
    {
        if (!results.Servers.TryGetValue(_serverKey, out var server))
        {
            server = new VoteResultsServer();
            results.Servers[_serverKey] = server;
        }

        return server;
    }

    [DataDefinition]
    private sealed partial class MapVoteResultsFile
    {
        [DataField("version")]
        public int Version = 1;

        [DataField("servers")]
        public Dictionary<string, VoteResultsServer> Servers = new();
    }

    [DataDefinition]
    private sealed partial class VoteResultsServer
    {
        [DataField("months")]
        public Dictionary<string, List<MapVoteGroupStats>> Months = new();
    }

    [DataDefinition]
    internal sealed partial class MapVoteGroupStats
    {
        [DataField("eligibleMaps")]
        public List<string> EligibleMaps = new();

        [DataField("voteCount")]
        public int VoteCount;

        [DataField("firstVoteAt")]
        public DateTime? FirstVoteAt;

        [DataField("lastVoteAt")]
        public DateTime? LastVoteAt;

        [DataField("votes")]
        public Dictionary<string, int> Votes = new();

        [DataField("winners")]
        public Dictionary<string, int> Winners = new();

        [DataField("finalSelectedMaps")]
        public Dictionary<string, int> FinalSelectedMaps = new();

        [DataField("rareRotationAppliedCount")]
        public int RareRotationAppliedCount;
    }
}

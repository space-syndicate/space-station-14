using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.MapRotation;

public sealed partial class CorvaxMapVoteStatsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resMan = default!;

    private static readonly ResPath VoteResultsPath = new("/corvax_map_vote_results.json");

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
            _log.Error($"{CCCVars.MapRotationServerKey.Name} must be a non-empty key containing only letters, digits, '_' or '-'. Corvax map vote statistics are disabled.");
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
            using var stream = _resMan.UserData.OpenRead(VoteResultsPath);
            var results = ReadVoteResults(stream);

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

    private MapVoteResultsFile ReadVoteResults(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var results = new MapVoteResultsFile();

        if (root.TryGetProperty("version", out var version) && version.TryGetInt32(out var versionValue))
            results.Version = versionValue;

        if (!root.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Object)
            return results;

        foreach (var serverProperty in servers.EnumerateObject())
        {
            if (serverProperty.Value.ValueKind != JsonValueKind.Object ||
                !serverProperty.Value.TryGetProperty("months", out var months) ||
                months.ValueKind != JsonValueKind.Object)
                continue;

            var server = new VoteResultsServer();
            results.Servers[serverProperty.Name] = server;

            foreach (var monthProperty in months.EnumerateObject())
            {
                var groups = new List<MapVoteGroupStats>();
                server.Months[monthProperty.Name] = groups;

                if (monthProperty.Value.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in monthProperty.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    if (item.TryGetProperty("voteCount", out _))
                        AddExistingGroup(groups, item);
                    else
                        AddLegacyVote(groups, item);
                }
            }
        }

        return results;
    }

    private static void AddExistingGroup(List<MapVoteGroupStats> groups, JsonElement item)
    {
        var eligibleMaps = ReadStringArray(item, "eligibleMaps");
        var voteCount = ReadInt(item, "voteCount");
        var firstVoteAt = ReadDateTime(item, "firstVoteAt");
        var lastVoteAt = ReadDateTime(item, "lastVoteAt");
        var votes = ReadIntDictionary(item, "votes");
        var winners = ReadIntDictionary(item, "winners");
        var finalSelectedMaps = ReadIntDictionary(item, "finalSelectedMaps");
        var rareRotationAppliedCount = ReadInt(item, "rareRotationAppliedCount");

        var group = GetOrCreateVoteGroup(groups, eligibleMaps);
        group.VoteCount += voteCount;
        group.FirstVoteAt = MinDate(group.FirstVoteAt, firstVoteAt);
        group.LastVoteAt = MaxDate(group.LastVoteAt, lastVoteAt);
        AddCounts(group.Votes, votes);
        AddCounts(group.Winners, winners);
        AddCounts(group.FinalSelectedMaps, finalSelectedMaps);
        group.RareRotationAppliedCount += rareRotationAppliedCount;
    }

    private static void AddLegacyVote(List<MapVoteGroupStats> groups, JsonElement item)
    {
        AddVoteToMonthGroups(
            groups,
            ReadDateTime(item, "startedAt") ?? DateTime.UtcNow,
            ReadStringArray(item, "eligibleMaps"),
            ReadIntDictionary(item, "votes"),
            ReadString(item, "winner"),
            ReadString(item, "finalSelectedMap"),
            ReadBool(item, "rareRotationApplied"));
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

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static DateTime? ReadDateTime(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return value.TryGetDateTime(out var result) ? result : null;
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

    private static bool ReadBool(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static List<string> ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => item != null)
            .Select(item => item!)
            .ToList();
    }

    private static Dictionary<string, int> ReadIntDictionary(JsonElement element, string property)
    {
        var dictionary = new Dictionary<string, int>();
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
            return dictionary;

        foreach (var item in value.EnumerateObject())
        {
            if (item.Value.TryGetInt32(out var count))
                dictionary[item.Name] = count;
        }

        return dictionary;
    }

    private void BackupCorruptVoteResults()
    {
        if (!_resMan.UserData.Exists(VoteResultsPath))
            return;

        var backup = new ResPath($"/corvax_map_vote_results.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.json");
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
            using (var stream = _resMan.UserData.OpenWrite(tempPath))
            {
                JsonSerializer.Serialize(stream, results, _jsonOptions);
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

    private sealed class MapVoteResultsFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, VoteResultsServer> Servers { get; set; } = new();
    }

    private sealed class VoteResultsServer
    {
        public Dictionary<string, List<MapVoteGroupStats>> Months { get; set; } = new();
    }

    internal sealed class MapVoteGroupStats
    {
        public List<string> EligibleMaps { get; set; } = new();
        public int VoteCount { get; set; }
        public DateTime? FirstVoteAt { get; set; }
        public DateTime? LastVoteAt { get; set; }
        public Dictionary<string, int> Votes { get; set; } = new();
        public Dictionary<string, int> Winners { get; set; } = new();
        public Dictionary<string, int> FinalSelectedMaps { get; set; } = new();
        public int RareRotationAppliedCount { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.Lobby;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Lobby;

/// <summary>
/// Caches the public SS14 hub status and returns only the servers requested by
/// the lobby UI. This keeps arbitrary HTTP access out of the sandboxed client.
/// </summary>
public sealed partial class LobbyServerHubSystem : EntitySystem
{
    private const string HubServersUrl = "https://hub.playss14.com/api/servers";
    // Must remain shorter than the client's 30-second refresh interval.
    // Equal intervals can make every second client request hit the old cache.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    [Dependency] private IHttpClientHolder _httpClientHolder = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly object _cacheLock = new();
    private IReadOnlyDictionary<string, LobbyServerHubStatus> _cache =
        new Dictionary<string, LobbyServerHubStatus>(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _cacheValidUntil;
    private Task<IReadOnlyDictionary<string, LobbyServerHubStatus>>? _refreshTask;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("corvax.lobby-server-hub");
        SubscribeNetworkEvent<LobbyServerHubStatusRequestEvent>(OnStatusRequest);
    }

    private async void OnStatusRequest(LobbyServerHubStatusRequestEvent request, EntitySessionEventArgs args)
    {
        var requested = request.Addresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var statuses = await GetStatuses();
        var response = requested
            .Where(statuses.ContainsKey)
            .Select(address => statuses[address])
            .ToArray();

        var currentServerAddress = _configuration.GetCVar(CVars.HubServerUrl);
        RaiseNetworkEvent(
            new LobbyServerHubStatusResponseEvent(response, currentServerAddress),
            args.SenderSession.Channel);
    }

    private Task<IReadOnlyDictionary<string, LobbyServerHubStatus>> GetStatuses()
    {
        lock (_cacheLock)
        {
            if (DateTimeOffset.UtcNow < _cacheValidUntil)
                return Task.FromResult(_cache);

            return _refreshTask ??= RefreshStatuses();
        }
    }

    private async Task<IReadOnlyDictionary<string, LobbyServerHubStatus>> RefreshStatuses()
    {
        IReadOnlyDictionary<string, LobbyServerHubStatus> result;
        try
        {
            using var timeout = new CancellationTokenSource(RequestTimeout);
            using var response = await _httpClientHolder.Client.GetAsync(HubServersUrl, timeout.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var servers = await JsonSerializer.DeserializeAsync<HubServer[]>(stream, cancellationToken: timeout.Token)
                ?? Array.Empty<HubServer>();

            result = servers
                .Where(server => !string.IsNullOrWhiteSpace(server.Address) && server.StatusData != null)
                .GroupBy(server => server.Address, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new LobbyServerHubStatus(
                        group.Key,
                        SanitizeServerName(group.First().StatusData!.Name),
                        group.First().StatusData!.Preset,
                        group.First().StatusData!.Players,
                        group.First().StatusData!.SoftMaxPlayers,
                        group.First().StatusData!.Map),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            _sawmill.Warning($"Unable to refresh lobby server online from the hub: {exception.Message}");
            lock (_cacheLock)
            {
                result = _cache;
            }
        }

        lock (_cacheLock)
        {
            _cache = result;
            _cacheValidUntil = DateTimeOffset.UtcNow + CacheLifetime;
            _refreshTask = null;
        }

        return result;
    }

    private static string SanitizeServerName(string name)
    {
        var result = new StringBuilder(name.Length);
        var lastWasSpace = true;

        foreach (var rune in name.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.OtherSymbol
                or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.EnclosingMark
                or UnicodeCategory.Format)
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune))
            {
                if (!lastWasSpace)
                    result.Append(' ');

                lastWasSpace = true;
                continue;
            }

            result.Append(rune.ToString());
            lastWasSpace = false;
        }

        return result.ToString().Trim();
    }

    private sealed class HubServer
    {
        [JsonPropertyName("address")]
        public string Address { get; init; } = string.Empty;

        [JsonPropertyName("statusData")]
        public HubStatusData? StatusData { get; init; }
    }

    private sealed class HubStatusData
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("preset")]
        public string Preset { get; init; } = string.Empty;

        [JsonPropertyName("map")]
        public string? Map { get; init; }

        [JsonPropertyName("players")]
        public int Players { get; init; }

        [JsonPropertyName("soft_max_players")]
        public int? SoftMaxPlayers { get; init; }
    }
}

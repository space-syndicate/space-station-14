using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.Lobby;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Lobby;

/// <summary>
/// Caches the public SS14 hub status and returns only the servers requested by
/// the lobby UI. This keeps arbitrary HTTP access out of the sandboxed client.
/// </summary>
public sealed partial class LobbyServerHubSystem : EntitySystem
{
    private const int MaxRequestedServers = 32;
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
        if (!_configuration.GetCVar(CCCVars.LobbyServerHubEnabled) ||
            args.SenderSession.Status is not (SessionStatus.Connected or SessionStatus.InGame))
        {
            return;
        }

        var requested = request.Addresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRequestedServers)
            .ToArray();

        var statuses = await GetStatuses();

        if (args.SenderSession.Status is not (SessionStatus.Connected or SessionStatus.InGame))
            return;

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
            var servers = await _httpClientHolder.Client.GetFromJsonAsync<HubServer[]>(
                HubServersUrl,
                timeout.Token) ?? [];

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
        var filtered = string.Concat(name.EnumerateRunes().Where(rune =>
            Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.OtherSymbol
                or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.EnclosingMark
                or UnicodeCategory.Format)));
        return string.Join(' ', filtered.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record HubServer(
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("statusData")] HubStatusData? StatusData);

    private sealed record HubStatusData(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("preset")] string Preset,
        [property: JsonPropertyName("map")] string? Map,
        [property: JsonPropertyName("players")] int Players,
        [property: JsonPropertyName("soft_max_players")] int? SoftMaxPlayers);
}

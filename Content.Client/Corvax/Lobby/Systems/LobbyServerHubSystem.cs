using System;
using System.Collections.Generic;
using Content.Shared.Corvax.Lobby;
using Robust.Shared.Timing;

namespace Content.Client.Corvax.Lobby.Systems;

/// <summary>
/// Requests hub status through the connected game server because sandboxed
/// content clients cannot make arbitrary HTTP requests.
/// </summary>
public sealed partial class LobbyServerHubSystem : EntitySystem
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<string, LobbyServerHubStatus> _statuses =
        new(StringComparer.OrdinalIgnoreCase);
    private TimeSpan _nextRequest;

    public int Revision { get; private set; }
    public string CurrentServerAddress { get; private set; } = string.Empty;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LobbyServerHubStatusResponseEvent>(OnStatusResponse);
    }

    public void RequestUpdate(string[] addresses)
    {
        if (_timing.RealTime < _nextRequest)
            return;

        _nextRequest = _timing.RealTime + RefreshInterval;
        RaiseNetworkEvent(new LobbyServerHubStatusRequestEvent(addresses));
    }

    public bool TryGetStatus(string address, out LobbyServerHubStatus status)
    {
        return _statuses.TryGetValue(address, out status!);
    }

    public bool IsCurrentServer(string address)
    {
        if (string.IsNullOrWhiteSpace(CurrentServerAddress))
            return false;

        return string.Equals(
            NormalizeAddress(address),
            NormalizeAddress(CurrentServerAddress),
            StringComparison.OrdinalIgnoreCase);
    }

    private void OnStatusResponse(LobbyServerHubStatusResponseEvent response)
    {
        CurrentServerAddress = response.CurrentServerAddress;
        _statuses.Clear();
        foreach (var status in response.Statuses)
            _statuses[status.Address] = status;

        Revision++;
    }

    private static string NormalizeAddress(string address)
    {
        return address.Trim().TrimEnd('/');
    }
}

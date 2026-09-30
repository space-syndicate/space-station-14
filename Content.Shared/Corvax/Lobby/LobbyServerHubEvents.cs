using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Lobby;

[Serializable, NetSerializable]
public sealed class LobbyServerHubStatusRequestEvent(string[] addresses) : EntityEventArgs
{
    public string[] Addresses { get; } = addresses;
}

[Serializable, NetSerializable]
public sealed class LobbyServerHubStatusResponseEvent(
    LobbyServerHubStatus[] statuses,
    string currentServerAddress) : EntityEventArgs
{
    public LobbyServerHubStatus[] Statuses { get; } = statuses;
    public string CurrentServerAddress { get; } = currentServerAddress;
}

[Serializable, NetSerializable]
public sealed class LobbyServerHubStatus(
    string address,
    string name,
    string preset,
    int players,
    int? softMaxPlayers,
    string? map)
{
    public string Address { get; } = address;
    public string Name { get; } = name;
    public string Preset { get; } = preset;
    public int Players { get; } = players;
    public int? SoftMaxPlayers { get; } = softMaxPlayers;
    public string? Map { get; } = map;
}

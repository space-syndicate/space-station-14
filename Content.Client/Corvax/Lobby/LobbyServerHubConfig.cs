using System;
using System.Linq;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Client.Corvax.Lobby;

/// <summary>
/// Resource-backed configuration for the lobby server hub.
/// </summary>
[DataDefinition]
public sealed partial class LobbyServerHubConfig
{
    [DataField]
    public List<LobbyServerEntry> Primary { get; private set; } = new();

    [DataField]
    public List<LobbyServerEntry> Subprojects { get; private set; } = new();

}

public enum LobbyServerSection : byte
{
    Primary,
    Subprojects,
}

[DataDefinition]
public sealed partial class LobbyServerEntry
{
    [DataField(required: true)]
    public string Address { get; private set; } = string.Empty;

    [DataField]
    public bool Adult { get; private set; }

    public bool TryGetAddress(out string address)
    {
        address = Address.Trim();
        var authorityStart = address.StartsWith("ss14://", StringComparison.OrdinalIgnoreCase)
            ? "ss14://".Length
            : address.StartsWith("ss14s://", StringComparison.OrdinalIgnoreCase)
                ? "ss14s://".Length
                : -1;
        if (authorityStart < 0)
            return false;

        var authorityEnd = address.IndexOf('/', authorityStart);
        var authority = address[authorityStart..(authorityEnd < 0 ? address.Length : authorityEnd)];
        return authority.Length > 0 && authority.All(character => !char.IsWhiteSpace(character));
    }
}

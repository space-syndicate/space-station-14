using Content.Shared.Security;
using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

/// <summary>
/// Show a list of wanted and suspected people from criminal records.
/// </summary>
[Serializable, NetSerializable]
public sealed class SecWatchUiState : BoundUserInterfaceState
{
    public readonly List<SecWatchEntry> Entries;

    public SecWatchUiState(List<SecWatchEntry> entries)
    {
        Entries = entries;
    }
}

/// <summary>
/// Entry for a person who is wanted or suspected.
/// </summary>
[Serializable, NetSerializable]
public record struct SecWatchEntry(string Name, string Job, SecurityStatus Status, string? Reason);
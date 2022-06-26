using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Icarus;

/// <summary>
/// Used for Icarus terminal activation
/// </summary>
[RegisterComponent]
public sealed class IcarusTerminalComponent : Component
{
    public static string FirstKey = "IcarusTerminal-firstKey";

    [DataField("firstKeySlot")]
    public ItemSlot FirstKeySlot = new();
}

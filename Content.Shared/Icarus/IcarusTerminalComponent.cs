using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Icarus;

/// <summary>
/// Used for Icarus terminal activation
/// </summary>
[RegisterComponent]
public sealed class IcarusTerminalComponent : Component
{
    protected override void Initialize()
    {
        base.Initialize();

        Owner.EnsureComponentWarn<ItemSlotsComponent>();
    }
}

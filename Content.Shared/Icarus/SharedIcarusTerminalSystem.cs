using System.Linq;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Icarus;

/// <summary>
/// Handle Icarus activation terminal
/// </summary>
public abstract class SharedIcarusTerminalSystem : EntitySystem
{
    public override void Initialize()
    {
    }

    public void Fire()
    {
        // TODO: Fire Icarus beam
    }

    public bool IsUnlocked(EntityUid uid)
    {
        return EntityManager.GetComponent<ItemSlotsComponent>(uid).Slots.Values.All(v => v.HasItem);
    }
}

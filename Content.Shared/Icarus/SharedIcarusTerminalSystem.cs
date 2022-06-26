using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Icarus;

/// <summary>
/// Handle Icarus activation terminal
/// </summary>
public abstract class SharedIcarusTerminalSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<IcarusTerminalComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<IcarusTerminalComponent, ComponentRemove>(OnComponentRemove);
    }
    public void Fire()
    {
        // TODO: Fire Icarus beam
    }

    public bool IsUnlocked(IcarusTerminalComponent comp)
    {
        return comp.FirstKeySlot.HasItem;
    }

    private void OnComponentInit(EntityUid uid, IcarusTerminalComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, IcarusTerminalComponent.FirstKey, component.FirstKeySlot);
    }

    private void OnComponentRemove(EntityUid uid, IcarusTerminalComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.FirstKeySlot);
    }
}

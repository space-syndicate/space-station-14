using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Labels.Components;

namespace Content.Shared._CorvaxNext.Medical.SmartFridge;

public abstract class SharedSmartFridgeSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;

    public List<SmartFridgeInventoryItem> GetInventoryClient(EntityUid uid,
        SmartFridgeComponent? smartFridgeComponent = null)
    {
        if (!Resolve(uid, ref smartFridgeComponent))
            return [];

        var inventory = new List<SmartFridgeInventoryItem>();

        inventory.AddRange(smartFridgeComponent.Inventory);

        return inventory;
    }

    protected List<SmartFridgeInventoryItem> GetInventory(EntityUid uid, SmartFridgeComponent? smartFridgeComponent = null)
    {
        if (!Resolve(uid, ref smartFridgeComponent))
            return [];

        var repeatedItems = new Dictionary<string, SmartFridgeInventoryItem>();
        for (var i = 0; i < smartFridgeComponent.NumSlots; i++)
        {
            var storageSlotId = SmartFridgeComponent.BaseStorageSlotId + i;

            var storedItem = _itemSlotsSystem.GetItemOrNull(uid, storageSlotId);

            if (storedItem == null)
                continue;

            string itemLabel;
            if (TryComp<LabelComponent>(storedItem, out var label) && !string.IsNullOrEmpty(label.CurrentLabel))
                itemLabel = label.CurrentLabel;
            else
                itemLabel = Name(storedItem.Value);

            if (repeatedItems.TryGetValue(itemLabel, out var item))
            {
                item.Quantity += 1;
                continue;
            }

            var meta = MetaData(storedItem.Value);

            repeatedItems.Add(itemLabel, new SmartFridgeInventoryItem(meta.EntityPrototype!, storageSlotId, itemLabel, 1));
        }

        return repeatedItems.Values.ToList();
    }
}

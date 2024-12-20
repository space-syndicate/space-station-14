using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Component = Robust.Shared.GameObjects.Component;

namespace Content.Shared._CorvaxNext.Medical.SmartFridge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SmartFridgeComponent : Component
{
    /// <summary>
    /// max slots in the SmartFridge, means they can store n items
    /// </summary>
    [DataField("numStorageSlots")]
    public int NumSlots = 100;

    [DataField, AutoNetworkedField]
    public ItemSlot FridgeSlots = new();

    [DataField]
    public List<string> StorageSlotIds = [];

    [DataField]
    public List<ItemSlot> StorageSlots = [];

    /// <summary>
    /// latest available inventory
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<SmartFridgeInventoryItem> Inventory = [];

    /// <summary>
    /// Prefix for automatically-generated slot name for storage, up to NumSlots.
    /// </summary>
    public static readonly string BaseStorageSlotId = "SmartFridge-storageSlot";

    /// <summary>
    /// what types of things can people store in here
    /// pill bottles, bottles, and food
    /// </summary>
    [DataField]
    public EntityWhitelist? StorageWhitelist;

    /// <summary>
    /// How long should the SmartFridge take to dispense something. In Seconds.
    /// </summary>
    [DataField]
    public float EjectDelay = 1.2f;

    /// <summary>
    /// If the SmartFridge is currently vending anything.
    /// </summary>
    [DataField]
    public bool Ejecting;

    [DataField]
    public float EjectAccumulator;
    public ItemSlot? SlotToEjectFrom;

    [DataField]
    // Grabbed from: https://github.com/tgstation/tgstation/blob/d34047a5ae911735e35cd44a210953c9563caa22/sound/machines/machine_vend.ogg
    public SoundSpecifier SoundVend = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg")
    {
        Params = new AudioParams
        {
            Volume = -4f,
            Variation = 0.15f,
        },
    };
}

[Serializable, NetSerializable]
public sealed class SmartFridgeInventoryItem(EntProtoId id, string storageSlotId, string itemName, FixedPoint2 quantity)
{
    public EntProtoId Id = id;
    public string StorageSlotId = storageSlotId;
    public string ItemName = itemName;
    public FixedPoint2 Quantity = quantity;
}

[Serializable, NetSerializable]
public enum SmartFridgeUiKey
{
    Key
}

// doing it here cuz idgaf
[Serializable, NetSerializable]
public sealed class SmartFridgeEjectMessage(string id) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
}

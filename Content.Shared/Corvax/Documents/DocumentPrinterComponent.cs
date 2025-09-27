using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Corvax.Documents;

[RegisterComponent]
public sealed partial class DocumentPrinterComponent : Component
{
    [DataField]
    public ItemSlot IdSlot = new();
}

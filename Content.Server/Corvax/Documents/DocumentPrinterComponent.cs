using Content.Shared.Containers.ItemSlots;

namespace Content.Server.Documents
{
    [RegisterComponent]
    public sealed partial class DocumentPrinterComponent : Component
    {
        [DataField("idSlot")]
        public ItemSlot IdSlot = new();
    }
}

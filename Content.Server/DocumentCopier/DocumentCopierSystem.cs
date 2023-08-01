using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Fax;
using Content.Server.Power.Components;
using Content.Server.UserInterface;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DocumentCopier;
using Content.Shared.Emag.Components;
using Content.Shared.Fax;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.DocumentCopier;

public sealed class DocumentCopierSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;

    private const string TargetPaperSlotId = "targetSheet";
    private const string SourcePaperSlotId = "sourceSheet";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DocumentCopierComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DocumentCopierComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DocumentCopierComponent, ComponentRemove>(OnComponentRemove);

        // On act
        SubscribeLocalEvent<DocumentCopierComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<DocumentCopierComponent, EntRemovedFromContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<DocumentCopierComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<DocumentCopierComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
    }

    private void OnMapInit(EntityUid uid, DocumentCopierComponent component, MapInitEvent args)
    {

    }

    private void OnComponentInit(EntityUid uid, DocumentCopierComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, TargetPaperSlotId, component.SourceSheet);
        _itemSlotsSystem.AddItemSlot(uid, SourcePaperSlotId, component.TargetSheet);
        UpdateAppearance(uid, component);
    }

    private void OnComponentRemove(EntityUid uid, DocumentCopierComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.TargetSheet);
        _itemSlotsSystem.RemoveItemSlot(uid, component.SourceSheet);
    }

    private void OnItemSlotChanged(EntityUid uid, DocumentCopierComponent component, ContainerModifiedMessage args)
    {
        // if (!component.Initialized)
        //     return;
        //
        // if (args.Container.ID != component.PaperSlot.ID)
        //     return;
        //
        // var isPaperInserted = component.PaperSlot.Item.HasValue;
        // if (isPaperInserted)
        // {
        //     component.InsertingTimeRemaining = component.InsertionTime;
        //     _itemSlotsSystem.SetLock(uid, component.PaperSlot, true);
        // }
        //
        UpdateUserInterface(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, DocumentCopierComponent component, ref PowerChangedEvent args)
    {
        var isInsertInterrupted = !args.Powered && component.InsertingTimeRemaining > 0;
        if (isInsertInterrupted)
        {
            component.InsertingTimeRemaining = 0f; // Reset animation

            // Drop from slot because animation did not play completely
            // _itemSlotsSystem.SetLock(uid, component.PaperSlot, false);
            // _itemSlotsSystem.TryEject(uid, component.PaperSlot, null, out var _, true);
        }

        var isPrintInterrupted = !args.Powered && component.PrintingTimeRemaining > 0;
        if (isPrintInterrupted)
        {
            component.PrintingTimeRemaining = 0f; // Reset animation
        }

        if (isInsertInterrupted || isPrintInterrupted)
            UpdateAppearance(uid, component);

        // _itemSlotsSystem.SetLock(uid, component.PaperSlot, !args.Powered); // Lock slot when power is off
    }



    private void OnToggleInterface(EntityUid uid, DocumentCopierComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateAppearance(uid, component);
    }

    // TODO: remake appearance to individual texture
    private void UpdateAppearance(EntityUid uid, DocumentCopierComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.InsertingTimeRemaining > 0)
            _appearanceSystem.SetData(uid, DocumentCopierVisuals.VisualState, DocumentCopierMachineVisualState.Inserting);
        else if (component.PrintingTimeRemaining > 0)
            _appearanceSystem.SetData(uid, DocumentCopierVisuals.VisualState, DocumentCopierMachineVisualState.Printing);
        else
            _appearanceSystem.SetData(uid, DocumentCopierVisuals.VisualState, DocumentCopierMachineVisualState.Normal);
    }

    private void UpdateUserInterface(EntityUid uid, DocumentCopierComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var isSourceDocumentInserted = component.SourceSheet.Item != null;
        var isTargetDocumentInserted = component.TargetSheet.Item != null;

        var state = new DocumentCopierUiState(isSourceDocumentInserted, isTargetDocumentInserted);
        _userInterface.TrySetUiState(uid, DocumentCopierUiKey.Key, state);
    }
}

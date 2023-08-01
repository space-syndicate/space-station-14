using Content.Server.DeviceNetwork.Systems;
using Content.Server.Fax;
using Content.Server.Paper;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.UserInterface;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DocumentCopier;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.DocumentCopier;

public sealed class DocumentCopierSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

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

        // UI
        SubscribeLocalEvent<DocumentCopierComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
        SubscribeLocalEvent<DocumentCopierComponent, DocumentCopierPrintMessage>(OnPrintButtonPressed);
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

    private void OnPrintButtonPressed(EntityUid uid, DocumentCopierComponent component, DocumentCopierPrintMessage args)
    {
        if (component.TargetSheet.Item.HasValue && TrySpawnPaper(uid, component))
        {
            EntityManager.DeleteEntity(component.TargetSheet.Item.Value);
        }

        UpdateUserInterface(uid, component);
    }

    private bool TrySpawnPaper(EntityUid uid, DocumentCopierComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.SourceSheet.Item == null || component.TargetSheet.Item == null)
            return false;

        var source = component.SourceSheet.Item;

        if (!TryComp<PaperComponent>(component.TargetSheet.Item, out var targetPaper))
            return false;
        if (targetPaper.StampState != null)
        {
            _popupSystem.PopupEntity("Target paper was already stamped, it can't being override", uid);
            return false;
        }

        const string entityToSpawn = "Paper";
        var target = EntityManager.SpawnEntity(entityToSpawn, Transform(uid).Coordinates);

        if (TryComp<PaperComponent>(source, out var sourcePaper))
        {
            if (sourcePaper.Content == "") // if source text is empty, it doesn't print
            {
                _popupSystem.PopupEntity("Source paper is empty, printing was automatically stopped", uid);
                return true;
            }

            _paperSystem.SetContent(target, sourcePaper.Content);

            // Apply stamps
            if (sourcePaper.StampState != null)
            {
                foreach (var stampedBy in sourcePaper.StampedBy)
                {
                    _paperSystem.TryStamp(target, stampedBy, sourcePaper.StampState);
                }
            }
        }

        if (TryComp<MetaDataComponent>(target, out var targetMetadata) &&
            TryComp<MetaDataComponent>(source, out var sourceMetadata))
            targetMetadata.EntityName = sourceMetadata.EntityName;

        return true;
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

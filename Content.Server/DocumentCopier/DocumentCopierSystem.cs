using Content.Server.Paper;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.UserInterface;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DocumentCopier;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.DocumentCopier;

public sealed class DocumentCopierSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DocumentCopierComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var documentCopier, out var receiver))
        {
            if (!receiver.Powered)
                continue;

            ProcessPrintingAnimation(uid, frameTime, documentCopier);
        }
    }

    private void ProcessPrintingAnimation(EntityUid uid, float frameTime, DocumentCopierComponent comp)
    {
        if (comp.PrintingTimeRemaining > 0)
        {
            comp.PrintingTimeRemaining -= frameTime;
            UpdateAppearance(uid, comp);

            var isAnimationEnd = comp.PrintingTimeRemaining <= 0;
            if (isAnimationEnd)
            {
                SpawnPaperFromQueue(uid, comp);
                UpdateUserInterface(uid, comp);
            }

            return;
        }

        if (comp.PrintingQueue.Count > 0)
        {
            comp.PrintingTimeRemaining = comp.PrintingTime;
            _audioSystem.PlayPvs(comp.PrintSound, uid);
        }
    }

    private void OnToggleInterface(EntityUid uid, DocumentCopierComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateAppearance(uid, component);
    }

    private void OnPrintButtonPressed(EntityUid uid, DocumentCopierComponent component, DocumentCopierPrintMessage args)
    {
        if (component.SourceSheet.Item == null || component.TargetSheet.Item == null)
            return;

        if (TryComp<PaperComponent>(component.TargetSheet.Item, out var targetPaper))
        {
            if (targetPaper.StampState != null)
            {
                _popupSystem.PopupEntity(Robust.Shared.Localization.Loc.GetString("document-copier-popup-stump-failed"), uid);
                return;
            }
        }

        var sendEntity = component.SourceSheet.Item;
        if (sendEntity == null)
            return;

        if (!TryComp<MetaDataComponent>(sendEntity, out var metadata) ||
            !TryComp<PaperComponent>(sendEntity, out var paper))
            return;

        var printout = new DocumentCopierPrintout(paper.Content, metadata.EntityName);

        if (paper.Content == "") // if source text is empty, it doesn't print
        {
            _popupSystem.PopupEntity(Robust.Shared.Localization.Loc.GetString("document-copier-popup-override-failed"), uid);
            return;
        }

        if (metadata.EntityPrototype != null)
        {
            printout.PrototypeId = metadata.EntityPrototype.ID;
        }

        if (paper.StampState != null)
        {
            printout.StampState = paper.StampState;
            printout.StampedBy.AddRange(paper.StampedBy);
        }

        component.PrintingQueue.Enqueue(printout);

        EntityManager.DeleteEntity(component.TargetSheet.Item.Value);
    }

    private void SpawnPaperFromQueue(EntityUid uid, DocumentCopierComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.PrintingQueue.Count == 0)
            return;

        var printout = component.PrintingQueue.Dequeue();

        var entityToSpawn = printout.PrototypeId.Length == 0 ? "Paper" : printout.PrototypeId;
        var printed = EntityManager.SpawnEntity(entityToSpawn, Transform(uid).Coordinates);

        if (TryComp<PaperComponent>(printed, out _))
        {
            _paperSystem.SetContent(printed, printout.Content);

            // Apply stamps
            if (printout.StampState != null)
            {
                foreach (var stampedBy in printout.StampedBy)
                {
                    _paperSystem.TryStamp(printed, stampedBy, printout.StampState);
                }
            }
        }

        if (TryComp<MetaDataComponent>(printed, out var metadata))
            metadata.EntityName = printout.Name;
    }

    // TODO: remake appearance to individual texture
    private void UpdateAppearance(EntityUid uid, DocumentCopierComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.InsertingTimeRemaining > 0)
        {
            _appearanceSystem.SetData(uid, DocumentCopierVisuals.VisualState,
                DocumentCopierMachineVisualState.Inserting);
        }
        else if (component.PrintingTimeRemaining > 0)
        {
            _appearanceSystem.SetData(uid, DocumentCopierVisuals.VisualState,
                DocumentCopierMachineVisualState.Printing);
        }
        else
        {
            _appearanceSystem.SetData(uid, DocumentCopierVisuals.VisualState, DocumentCopierMachineVisualState.Normal);
        }
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

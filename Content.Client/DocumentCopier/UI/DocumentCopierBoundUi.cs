using Content.Client.Fax.UI;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DocumentCopier;
using Content.Shared.Fax;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.DocumentCopier.UI;

[UsedImplicitly]
public sealed class DocumentCopierBoundUi : BoundUserInterface
{
    public static string SourceDocumentSlotId = "targetSheet";
    public static string TargetDocumentSlotId = "sourceSheet";

    private DocumentCopierWindow? _window;

    public DocumentCopierBoundUi(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new DocumentCopierWindow();
        _window.OpenCentered();

        _window.OnClose += Close;
        _window.PrintButtonPressed += OnPrintButtonPressed;
        _window.SourceDocumentButtonPressed += OnSourceDocumentButtonPressed;
        _window.TargetDocumentButtonPressed += OnTargetDocumentButtonPressed;
    }

    private void OnSourceDocumentButtonPressed()
    {
        SendMessage(new ItemSlotButtonPressedEvent(SourceDocumentSlotId));
    }

    private void OnTargetDocumentButtonPressed()
    {
        SendMessage(new ItemSlotButtonPressedEvent(TargetDocumentSlotId));
    }

    private void OnPrintButtonPressed()
    {
        SendMessage(new DocumentCopierPrintMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not DocumentCopierUiState cast)
            return;

        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}

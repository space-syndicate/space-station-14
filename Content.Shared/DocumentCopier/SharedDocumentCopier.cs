using Robust.Shared.Serialization;

namespace Content.Shared.DocumentCopier;

[Serializable, NetSerializable]
public enum DocumentCopierUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DocumentCopierUiState : BoundUserInterfaceState
{
    public bool IsSourceDocumentInserted;
    public bool IsTargetDocumentInserted;
    public bool IsCanPrintDocument;

    public DocumentCopierUiState(
        bool isSourceDocumentInserted,
        bool isTargetDocumentInserted)
    {
        IsSourceDocumentInserted = isSourceDocumentInserted;
        IsTargetDocumentInserted = isTargetDocumentInserted;

        IsCanPrintDocument = IsSourceDocumentInserted && IsTargetDocumentInserted;
    }
}

[Serializable, NetSerializable]
public sealed class DocumentCopierPrintMessage : BoundUserInterfaceMessage
{
}

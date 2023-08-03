using Robust.Shared.Serialization;

namespace Content.Shared.DocumentCopier;

[Serializable, NetSerializable]
public enum DocumentCopierVisuals : byte
{
    VisualState,
}

[Serializable, NetSerializable]
public enum DocumentCopierMachineVisualState : byte
{
    Normal,
    Inserting,
    Printing
}

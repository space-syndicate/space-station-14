using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Footprints;

[Serializable, NetSerializable]
public enum FootprintVisuals : byte
{
    BareFootprint,
    ShoesPrint,
    SuitPrint,
    Dragging
}

[Serializable, NetSerializable]
public enum FootprintVisualState : byte
{
    State,
    Color
}

[Serializable, NetSerializable]
public enum FootprintVisualLayers : byte
{
    Print
}

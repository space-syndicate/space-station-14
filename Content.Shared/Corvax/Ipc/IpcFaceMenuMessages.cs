using Content.Shared.UserInterface;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Ipc;

[Serializable, NetSerializable]
public sealed class IpcFaceSelectMessage : BoundUserInterfaceMessage
{
    public readonly string State;
    public IpcFaceSelectMessage(string state)
    {
        State = state;
    }
}

[Serializable, NetSerializable]
public sealed class IpcFaceBuiState : BoundUserInterfaceState
{
    public readonly string Profile;
    public readonly string Selected;
    public IpcFaceBuiState(string profile, string selected)
    {
        Profile = profile;
        Selected = selected;
    }
}

[NetSerializable, Serializable]
public enum IpcFaceUiKey : byte
{
    Face
}

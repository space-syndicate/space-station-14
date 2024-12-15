using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Standing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LayingDownComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float StandingUpTime { get; set; } = 1f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float SpeedModify { get; set; } = 0.25f;

    [ViewVariables, AutoNetworkedField]
    public bool DrawDowned { get; set; } = false;

    [ViewVariables]
    public int? OriginalDrawDepth { get; set; } = (int)DrawDepth.DrawDepth.Mobs;
}
[Serializable, NetSerializable]
public sealed class ChangeLayingDownEvent : CancellableEntityEventArgs;

/*
[Serializable, NetSerializable]
public sealed class CheckAutoGetUpEvent(NetEntity user) : CancellableEntityEventArgs
{
    public NetEntity User = user;
}
*/

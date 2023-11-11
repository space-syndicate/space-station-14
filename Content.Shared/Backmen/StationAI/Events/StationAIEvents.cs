using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.StationAI.Events;

public sealed partial class AIHealthOverlayEvent : InstantActionEvent
{
    public AIHealthOverlayEvent()
    {

    }
}

[Serializable, NetSerializable]
public sealed class NetworkedAIHealthOverlayEvent : EntityEventArgs
{
    public NetEntity Performer = NetEntity.Invalid;

    public NetworkedAIHealthOverlayEvent(NetEntity performer)
    {
        Performer = performer;
    }
}


[Serializable, NetSerializable]
public sealed class AICameraListMessage : BoundUserInterfaceMessage
{
    public NetEntity Owner;

    public AICameraListMessage(NetEntity owner)
    {
        Owner = owner;
    }
}

[Serializable, NetSerializable]
public sealed class AICameraWarpMessage : BoundUserInterfaceMessage
{
    public NetEntity Owner;
    public NetEntity Camera;

    public AICameraWarpMessage(NetEntity owner, NetEntity camera)
    {
        Owner = owner;
        Camera = camera;
    }
}

[Serializable, NetSerializable]
public sealed class AIBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<NetEntity> Cameras = new List<NetEntity>();

    public AIBoundUserInterfaceState(List<NetEntity> cameras)
    {
        Cameras = cameras;
    }
}

public sealed partial class InnateAfterInteractActionEvent : EntityTargetActionEvent
{
    public EntityUid Item;

    public InnateAfterInteractActionEvent(EntityUid item)
    {
        Item = item;
    }
}

public sealed partial class InnateBeforeInteractActionEvent : EntityTargetActionEvent
{
    public EntityUid Item;

    public InnateBeforeInteractActionEvent(EntityUid item)
    {
        Item = item;
    }
}

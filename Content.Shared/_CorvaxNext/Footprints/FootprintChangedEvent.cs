using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Footprints;

[Serializable, NetSerializable]
public sealed class FootprintChangedEvent(NetEntity entity) : EntityEventArgs
{
    public NetEntity Entity = entity;
}

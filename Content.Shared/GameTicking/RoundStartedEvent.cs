using Robust.Shared.Serialization;

namespace Content.Shared.GameTicking
{
    [Serializable, NetSerializable]
    public class RoundStartedEvent : EntityEventArgs
    {
    }
}

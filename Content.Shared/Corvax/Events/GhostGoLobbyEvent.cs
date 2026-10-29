using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Events;

[Serializable, NetSerializable]
public sealed class GhostGoLobbyEvent : EntityEventArgs;

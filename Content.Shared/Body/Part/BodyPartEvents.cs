using Content.Shared.Humanoid;

namespace Content.Shared.Body.Part;

[ByRefEvent]
public readonly record struct BodyPartAddedEvent(string Slot, Entity<BodyPartComponent> Part);

// start-_CorvaxNext: surgery
// Kind of a clone of the above for surgical reattachment specifically.
[ByRefEvent]
public readonly record struct BodyPartAttachedEvent(Entity<BodyPartComponent> Part);
// end-_CorvaxNext: surgery

[ByRefEvent]
public readonly record struct BodyPartRemovedEvent(string Slot, Entity<BodyPartComponent> Part);

// start-_CorvaxNext: surgery
// Kind of a clone of the above for any instances where we call DropPart(), reasoning being that RemovedEvent fires off
// a lot more often than what I'd like due to PVS.
[ByRefEvent]
public readonly record struct BodyPartDroppedEvent(Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartEnableChangedEvent(bool Enabled);

[ByRefEvent]
public readonly record struct BodyPartEnabledEvent(Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartDisabledEvent(Entity<BodyPartComponent> Part);
// end-_CorvaxNext: surgery

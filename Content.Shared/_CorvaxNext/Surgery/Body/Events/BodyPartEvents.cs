using Content.Shared.Body.Part;

namespace Content.Shared._CorvaxNext.Surgery.Body.Events;

/// <summary>
/// Raised on an entity when attempting to remove a body part.
/// </summary>
[ByRefEvent]
public readonly record struct AmputateAttemptEvent(EntityUid Part);

// start-_CorvaxNext: surgery
// Kind of a clone of BodyPartAddedEvent for surgical reattachment specifically.
[ByRefEvent]
public readonly record struct BodyPartAttachedEvent(Entity<BodyPartComponent> Part);
// end-_CorvaxNext: surgery

// start-_CorvaxNext: surgery
// Kind of a clone of BodyPartRemovedEvent for any instances where we call DropPart(), reasoning being that RemovedEvent fires off
// a lot more often than what I'd like due to PVS.
[ByRefEvent]
public readonly record struct BodyPartDroppedEvent(Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartEnableChangedEvent(bool Enabled);

[ByRefEvent]
public readonly record struct BodyPartEnabledEvent(Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartDisabledEvent(Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartAddedEvent(string Slot, Entity<BodyPartComponent> Part);

[ByRefEvent]
public readonly record struct BodyPartRemovedEvent(string Slot, Entity<BodyPartComponent> Part);

public readonly record struct BodyPartComponentsModifyEvent(EntityUid Body, bool Add);
// end-_CorvaxNext: surgery

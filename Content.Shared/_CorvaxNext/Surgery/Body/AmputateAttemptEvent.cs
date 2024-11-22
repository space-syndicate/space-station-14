namespace Content.Shared.Body.Events;

/// <summary>
/// Raised on an entity when attempting to remove a body part.
/// </summary>
[ByRefEvent]
public readonly record struct AmputateAttemptEvent(EntityUid Part);

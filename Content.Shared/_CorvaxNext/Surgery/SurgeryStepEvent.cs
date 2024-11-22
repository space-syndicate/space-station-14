namespace Content.Shared._CorvaxNext.Surgery;

/// <summary>
///     Raised on the step entity.
/// </summary>
[ByRefEvent]
public record struct SurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, List<EntityUid> Tools, EntityUid Surgery);

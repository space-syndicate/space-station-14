using Content.Shared.Damage;

namespace Content.Shared.Medical.Surgery;

/// <summary>
///     Raised on the target entity.
/// </summary>
[ByRefEvent]
public record struct SurgeryStepDamageChangeEvent(EntityUid User, EntityUid Body, EntityUid Part, EntityUid Step);

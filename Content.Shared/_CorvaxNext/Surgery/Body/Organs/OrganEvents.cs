using Content.Shared.Body.Organ;
namespace Content.Shared._CorvaxNext.Surgery.Body.Organs;

public readonly record struct OrganComponentsModifyEvent(EntityUid Body, bool Add);

[ByRefEvent]
public readonly record struct OrganEnableChangedEvent(bool Enabled);

[ByRefEvent]
public readonly record struct OrganEnabledEvent(Entity<OrganComponent> Organ);

[ByRefEvent]
public readonly record struct OrganDisabledEvent(Entity<OrganComponent> Organ);

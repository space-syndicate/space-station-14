using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Content.Shared._CorvaxNext.Resomi.Abilities.Hearing;

namespace Content.Shared._CorvaxNext.Resomi;

public sealed partial class SwitchAgillityActionEvent : InstantActionEvent;

public sealed partial class ListenUpActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class ListenUpDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Rises when the action state changes
/// </summary>
/// <param name="action"> Entity of Action that we want change the state</param>
/// <param name="toggled"> </param>
[ByRefEvent]
public readonly record struct SwitchAgillity(Entity<BaseActionComponent> action, bool toggled);

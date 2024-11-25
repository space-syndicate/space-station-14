using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Resomi;

public sealed partial class SwitchAgillityActionEvent : InstantActionEvent;

/// <summary>
/// Rises when the action state changes
/// </summary>
/// <param name="action"> Entity of Action that we want change the state</param>
/// <param name="toggled"> </param>
[ByRefEvent]
public readonly record struct SwitchAgillity(Entity<BaseActionComponent> action, bool toggled);

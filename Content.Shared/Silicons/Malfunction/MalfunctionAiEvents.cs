using Content.Shared.Actions;

namespace Content.Shared.Silicons.Malfunction;

/// <summary>
/// Targets an APC and tries to hack it: turns off the breaker and drains its battery.
/// </summary>
public sealed partial class MalfHackApcEvent : EntityTargetActionEvent;

/// <summary>
/// Targets a machine and causes a small overload explosion.
/// </summary>
public sealed partial class MalfOverloadMachineEvent : EntityTargetActionEvent;

/// <summary>
/// Causes a station-wide blackout: shuts off APC breakers across the station for a short time.
/// </summary>
public sealed partial class MalfBlackoutEvent : InstantActionEvent;

/// <summary>
/// Arms the Doomsday device. Handled by the Malfunction AI rule.
/// </summary>
public sealed partial class MalfDoomsdayEvent : InstantActionEvent;

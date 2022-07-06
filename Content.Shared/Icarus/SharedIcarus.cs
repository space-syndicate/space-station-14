namespace Content.Shared.Icarus;

public enum IcarusTerminalStatus : byte
{
    AWAIT_DISKS,
    FIRE_READY,
    FIRE_PREPARING,
    COOLDOWN
}

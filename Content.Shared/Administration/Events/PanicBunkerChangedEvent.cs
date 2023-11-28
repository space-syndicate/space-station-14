using Robust.Shared.Serialization;

namespace Content.Shared.Administration.Events;

[Serializable, NetSerializable]
public sealed class PanicBunkerStatus
{
    public bool Enabled;
    public bool DisableWithAdmins;
    public bool EnableWithoutAdmins;
    public bool CountDeadminnedAdmins;
    public bool ShowReason;
    public int MinAccountAgeHours;
    public int MinOverallHours;
    public bool DenyVpn; // Corvax-VPNGuard
}

[Serializable, NetSerializable]
public sealed class PanicBunkerChangedEvent : EntityEventArgs
{
    public PanicBunkerStatus Status;

    public PanicBunkerChangedEvent(PanicBunkerStatus status)
    {
        Status = status;
    }
}

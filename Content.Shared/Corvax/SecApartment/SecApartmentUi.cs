using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Serialization;

namespace Content.Shared.SecApartment;

[Serializable, NetSerializable]
public enum SecApartmentUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SecApartmentUpdateState : BoundUserInterfaceState
{
    public string StationName { get; }
    public List<CrewMemberInfo> SecurityCrew { get; }
    public List<CrewMemberInfo> UnassignedSecurity { get; }
    public List<Squad> Squads { get; }

    public SecApartmentUpdateState(
        string stationName,
        List<CrewMemberInfo> securityCrew,
        List<CrewMemberInfo> unassignedSecurity,
        List<Squad> squads)
    {
        StationName = stationName;
        SecurityCrew = securityCrew;
        UnassignedSecurity = unassignedSecurity;
        Squads = squads;
    }
}

[Serializable, NetSerializable]
public sealed class SensorStatusUpdateState : BoundUserInterfaceState
{
    public Dictionary<string, SuitSensorStatus?> MemberStatuses { get; }
    public Dictionary<string, (string Location, bool HasLocation)> SquadLocations { get; }

    public SensorStatusUpdateState(
        Dictionary<string, SuitSensorStatus?> memberStatuses,
        Dictionary<string, (string Location, bool HasLocation)> squadLocations)
    {
        MemberStatuses = memberStatuses;
        SquadLocations = squadLocations;
    }
}

[Serializable, NetSerializable]
public sealed class CreateSquadMessage : BoundUserInterfaceMessage
{
    public string SquadName { get; }

    public CreateSquadMessage(string squadName)
    {
        SquadName = squadName;
    }
}

[Serializable, NetSerializable]
public sealed class DeleteSquadMessage : BoundUserInterfaceMessage
{
    public string SquadId { get; }

    public DeleteSquadMessage(string squadId)
    {
        SquadId = squadId;
    }
}

[Serializable, NetSerializable]
public sealed class RenameSquadMessage : BoundUserInterfaceMessage
{
    public string SquadId { get; }
    public string NewName { get; }

    public RenameSquadMessage(string squadId, string newName)
    {
        SquadId = squadId;
        NewName = newName;
    }
}

[Serializable, NetSerializable]
public sealed class UpdateSquadDescriptionMessage : BoundUserInterfaceMessage
{
    public string SquadId { get; }
    public string Description { get; }

    public UpdateSquadDescriptionMessage(string squadId, string description)
    {
        SquadId = squadId;
        Description = description;
    }
}

[Serializable, NetSerializable]
public sealed class AddMemberToSquadMessage : BoundUserInterfaceMessage
{
    public string SquadId { get; }
    public string MemberId { get; }

    public AddMemberToSquadMessage(string squadId, string memberId)
    {
        SquadId = squadId;
        MemberId = memberId;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveMemberFromSquadMessage : BoundUserInterfaceMessage
{
    public string SquadId { get; }
    public string MemberId { get; }

    public RemoveMemberFromSquadMessage(string squadId, string memberId)
    {
        SquadId = squadId;
        MemberId = memberId;
    }
}

[Serializable, NetSerializable]
public sealed class ChangeSquadIconMessage : BoundUserInterfaceMessage
{
    public string SquadId { get; }
    public SquadIconNum IconId { get; }

    public ChangeSquadIconMessage(string squadId, SquadIconNum iconId)
    {
        SquadId = squadId;
        IconId = iconId;
    }
}

[Serializable, NetSerializable]
public sealed class ChangeSquadStatusMessage : BoundUserInterfaceMessage
{
    public string SquadId { get; }
    public SquadStatus Status { get; }

    public ChangeSquadStatusMessage(string squadId, SquadStatus status)
    {
        SquadId = squadId;
        Status = status;
    }
}

[Serializable, NetSerializable]
public sealed class TimerUpdateState : BoundUserInterfaceState
{
    public List<TimerEntry> Timers { get; }

    public TimerUpdateState(List<TimerEntry> timers)
    {
        Timers = timers;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveTimerMessage : BoundUserInterfaceMessage
{
    public NetEntity TimerUid { get; }

    public RemoveTimerMessage(NetEntity timerUid)
    {
        TimerUid = timerUid;
    }
}

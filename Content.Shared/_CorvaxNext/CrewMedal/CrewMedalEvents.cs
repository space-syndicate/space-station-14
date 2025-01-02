using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.CrewMedal;

/// <summary>
/// Enum representing the key for the Crew Medal user interface.
/// </summary>
[Serializable, NetSerializable]
public enum CrewMedalUiKey : byte
{
    Key
}

/// <summary>
/// Message sent when the reason for the medal is changed via the user interface.
/// </summary>
[Serializable, NetSerializable]
public sealed class CrewMedalReasonChangedMessage(string Reason) : BoundUserInterfaceMessage
{
    /// <summary>
    /// The new reason for the medal.
    /// </summary>
    public string Reason { get; } = Reason;
}

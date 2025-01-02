using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxNext.CrewMedal;

/// <summary>
/// Component for a medal that can be awarded to a player and 
/// will be displayed in the final round summary screen.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CrewMedalComponent : Component
{
    /// <summary>
    /// The name of the recipient of the award.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public string Recipient = string.Empty;

    /// <summary>
    /// The reason for the award. Can be set before the medal is awarded.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public string Reason = string.Empty;

    /// <summary>
    /// If <c>true</c>, the medal is considered awarded, and the reason can no longer be changed.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public bool Awarded;

    /// <summary>
    /// The maximum number of characters allowed for the reason.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public int MaxCharacters = 50;
}

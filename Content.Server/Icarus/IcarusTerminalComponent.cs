namespace Content.Server.Icarus;

/// <summary>
/// Used to store Icarus terminal keys
/// </summary>
[RegisterComponent]
public sealed class IcarusTerminalComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("totalKeys")]
    public int TotalKeys = 3;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("activatedKeys")]
    public int ActivatedKeys = 0;
}

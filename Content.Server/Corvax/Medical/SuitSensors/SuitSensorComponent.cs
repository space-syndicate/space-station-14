using Content.Shared.Damage;

namespace Content.Server.Medical.SuitSensors;

public sealed partial class SuitSensorComponent
{
    /// <summary>
    /// Amount of damage that will be caused.
    /// </summary>
    [DataField]
    public DamageSpecifier DamageBonus = new()
    {
        DamageDict = new()
        {
            { "Cellular", 50 }
        }
    };

    /// <summary>
    /// Chance for the damage bonus to occur.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CellularDamageChance = 0.0001f;
}

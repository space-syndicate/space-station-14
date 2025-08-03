using Content.Shared.Damage;
using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Random;

namespace Content.Server.Medical.SuitSensors;

public sealed partial class SuitSensorSystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    private bool CellularDamageAttempt(Entity<SuitSensorComponent> sensors, SuitSensorMode mode, EntityUid userUid)
    {
        if (_random.Prob(sensors.Comp.CellularDamageChance) && mode != SuitSensorMode.SensorOff)
        {
            _damageableSystem.TryChangeDamage(userUid, sensors.Comp.DamageBonus, true);
            return true;
        }

        return false;
    }
}

using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._CorvaxNext.VoxRaiders.EntitySystems;

public sealed class BatteryToggleSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;

    public override void Update(float frameTime)
    {
        var query = AllEntityQuery<BatteryToggleComponent, ItemToggleComponent>();
        while (query.MoveNext(out var entity, out var battery, out var toggle))
        {
            var activated = _toggle.IsActivated((entity, toggle));

            battery.Charge = Math.Clamp(battery.Charge + (activated ? -frameTime : frameTime), 0, battery.MaxCharge);

            if (battery.Charge <= 0)
                _toggle.TryDeactivate((entity, toggle), predicted: false);
        }
    }
}

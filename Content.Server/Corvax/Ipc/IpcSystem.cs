using Content.Server.PowerCell;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Corvax.Ipc;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;

namespace Content.Server.Corvax.Ipc;

public sealed class IpcSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedBatteryDrainerSystem _batteryDrainer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IpcComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IpcComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<IpcComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<IpcComponent, ToggleDrainActionEvent>(OnToggleAction);
    }

    private void OnMapInit(EntityUid uid, IpcComponent component, MapInitEvent args)
    {
        UpdateBatteryAlert((uid, component));
        _action.AddAction(uid, ref component.ActionEntity, component.DrainBatteryAction);
    }

    private void OnComponentShutdown(EntityUid uid, IpcComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionEntity);
    }

    private void OnPowerCellChanged(EntityUid uid, IpcComponent component, PowerCellChangedEvent args)
    {
        if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        UpdateBatteryAlert((uid, component));

    }
    private void OnToggleAction(EntityUid uid, IpcComponent component, ToggleDrainActionEvent args)
    {
        if (args.Handled)
            return;

        component.DrainActivated = !component.DrainActivated;
        _action.SetToggled(component.ActionEntity, component.DrainActivated);
        args.Handled = true;

        if (component.DrainActivated && _powerCell.TryGetBatteryFromSlot(uid, out var battery, out var _))
        {
            EnsureComp<BatteryDrainerComponent>(uid);
            _batteryDrainer.SetBattery(uid, battery);
        }
        else
            RemComp<BatteryDrainerComponent>(uid);

        var message = component.DrainActivated ? "ipc-component-ready" : "ipc-component-disabled";
        _popup.PopupEntity(Loc.GetString(message), uid, uid);
    }

    private void UpdateBatteryAlert(Entity<IpcComponent> ent, PowerCellSlotComponent? slot = null)
    {


        if (!_powerCell.TryGetBatteryFromSlot(ent, out var battery, slot) || battery.CurrentCharge / battery.MaxCharge < 0.01f)
        {
            _alerts.ClearAlert(ent, ent.Comp.BatteryAlert);
            _alerts.ShowAlert(ent, ent.Comp.NoBatteryAlert);
            EnsureComp<ForcedSleepingComponent>(ent);
            return;
        }

        var chargePercent = (short) MathF.Round(battery.CurrentCharge / battery.MaxCharge * 10f);

        if (chargePercent == 0 && _powerCell.HasDrawCharge(ent, cell: slot))
            chargePercent = 1;

        RemComp<ForcedSleepingComponent>(ent);
        _sleeping.TryWaking(ent.Owner);

        _alerts.ClearAlert(ent, ent.Comp.NoBatteryAlert);
        _alerts.ShowAlert(ent, ent.Comp.BatteryAlert, chargePercent);
    }
}

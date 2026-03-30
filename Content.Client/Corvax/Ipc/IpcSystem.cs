using Content.Shared.Alert;
using Content.Shared.Corvax.Ipc;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Corvax.Ipc;
public sealed class IpcSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    private static readonly TimeSpan AlertUpdateDelay = TimeSpan.FromSeconds(0.5f);
    private TimeSpan _nextAlertUpdate = TimeSpan.Zero;
    private EntityQuery<IpcComponent> _ipcQuery;
    public override void Initialize()
    {
        base.Initialize();
        _ipcQuery = GetEntityQuery<IpcComponent>();
        SubscribeLocalEvent<IpcComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<IpcComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<IpcComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<IpcComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
    }
    private void OnPowerCellChanged(EntityUid uid, IpcComponent component, ref PowerCellChangedEvent args)
    {
        if (_player.LocalEntity != uid)
            return;
        UpdateBatteryAlert((uid, component));
    }
    private void OnPowerCellEmpty(EntityUid uid, IpcComponent component, ref PowerCellSlotEmptyEvent args)
    {
        if (_player.LocalEntity != uid)
            return;
        UpdateBatteryAlert((uid, component));
    }
    private void OnPlayerAttached(Entity<IpcComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        UpdateBatteryAlert(ent);
    }
    private void OnPlayerDetached(Entity<IpcComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
        _alerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
    }
    private void UpdateBatteryAlert(Entity<IpcComponent> ent)
    {
        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery)
            || battery.Value.Comp.MaxCharge <= 0
            || _battery.GetCharge(battery.Value.AsNullable()) / battery.Value.Comp.MaxCharge < 0.01f)
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
            _alerts.ShowAlert(ent.Owner, ent.Comp.NoBatteryAlert);
            return;
        }
        var chargePercent = (short)MathF.Round(
            _battery.GetCharge(battery.Value.AsNullable()) / battery.Value.Comp.MaxCharge * 10f);
        if (chargePercent == 0 && _powerCell.HasDrawCharge(ent.Owner))
            chargePercent = 1;
        _alerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
        _alerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, chargePercent);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_player.LocalEntity is not { } localPlayer)
            return;
        var curTime = _timing.CurTime;
        if (curTime < _nextAlertUpdate)
            return;
        _nextAlertUpdate = curTime + AlertUpdateDelay;
        if (!_ipcQuery.TryComp(localPlayer, out var ipc))
            return;
        UpdateBatteryAlert((localPlayer, ipc));
    }
}

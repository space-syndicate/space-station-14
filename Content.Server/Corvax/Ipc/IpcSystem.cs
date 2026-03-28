using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Corvax.Ipc;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Sound.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.Ipc;

public sealed partial class IpcSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedBatteryDrainerSystem _batteryDrainer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IpcComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IpcComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<IpcComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<IpcComponent, ToggleDrainActionEvent>(OnToggleAction);
        SubscribeLocalEvent<IpcComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<IpcComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<IpcComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<IpcComponent, OpenIpcFaceActionEvent>(OnOpenFaceAction);
        Subs.BuiEvents<IpcComponent>(IpcFaceUiKey.Face, subs =>
        {
            subs.Event<IpcFaceSelectMessage>(OnFaceSelected);
        });
        SubscribeLocalEvent<IpcComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<IpcComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsSlowedByBattery)
                continue;
            if (_powerCell.TryGetBatteryFromSlot(uid, out var battery)
                && _battery.GetCharge(battery.Value.AsNullable()) / battery.Value.Comp.MaxCharge >= 0.01f)
            {
                comp.IsSlowedByBattery = false;
                _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            }
        }
    }
    private void OnPowerCellEmpty(EntityUid uid, IpcComponent component, ref PowerCellSlotEmptyEvent args)
    {
        UpdateBatteryAlert((uid, component));
    }
    private void OnMapInit(EntityUid uid, IpcComponent component, MapInitEvent args)
    {
        UpdateBatteryAlert((uid, component));
        _action.AddAction(uid, ref component.ActionEntity, component.DrainBatteryAction);
        _action.AddAction(uid, ref component.ChangeFaceActionEntity, component.ChangeFaceAction);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);

        if (!HasComp<VisualBodyComponent>(uid))
            return;
        if (_visualBody.TryGatherMarkingsData(uid, null, out _, out _, out var applied)
            && applied.TryGetValue("Head", out var headMarkings)
            && headMarkings.TryGetValue(HumanoidVisualLayers.Snout, out var snoutMarkings)
            && snoutMarkings.Count > 0)
        {
            component.SelectedFace = snoutMarkings[0].MarkingId;
            Dirty(uid, component);
        }
    }
    private void OnComponentShutdown(EntityUid uid, IpcComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionEntity);
        _action.RemoveAction(uid, component.ChangeFaceActionEntity);
    }
    private void OnPowerCellChanged(EntityUid uid, IpcComponent component, ref PowerCellChangedEvent args)
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

        if (component.DrainActivated && _powerCell.TryGetBatteryFromSlot(uid, out var battery))
        {
            EnsureComp<BatteryDrainerComponent>(uid);
            _batteryDrainer.SetBattery(uid, battery.Value.Owner);
        }
        else
            RemComp<BatteryDrainerComponent>(uid);

        var message = component.DrainActivated ? "ipc-component-ready" : "ipc-component-disabled";
        _popup.PopupEntity(Loc.GetString(message), uid, uid);
    }
    private void UpdateBatteryAlert(Entity<IpcComponent> ent)
    {
        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent.Owner);
    }
    private void OnRefreshMovementSpeedModifiers(EntityUid uid, IpcComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!_powerCell.TryGetBatteryFromSlot(uid, out var battery)
            || battery.Value.Comp.MaxCharge <= 0
            || _battery.GetCharge(battery.Value.AsNullable()) / battery.Value.Comp.MaxCharge < 0.01f)
        {
            args.ModifySpeed(0.2f);
            comp.IsSlowedByBattery = true;
        }
        else
        {
            comp.IsSlowedByBattery = false;
        }
    }
    private void OnOpenFaceAction(EntityUid uid, IpcComponent comp, OpenIpcFaceActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        _ui.SetUiState(uid, IpcFaceUiKey.Face, new IpcFaceBuiState(comp.FaceProfile, comp.SelectedFace));
        _ui.TryToggleUi(uid, IpcFaceUiKey.Face, actor.PlayerSession);
        args.Handled = true;
    }
    private void OnFaceSelected(Entity<IpcComponent> ent, ref IpcFaceSelectMessage msg)
    {
        if (!_prototype.TryIndex<IpcFaceProfilePrototype>(ent.Comp.FaceProfile, out var faceProfile)
            || !faceProfile.Faces.Contains(msg.State))
            return;
        if (_visualBody.TryGatherMarkingsData(ent.Owner, null, out var profiles, out var markings, out var applied))
        {
            if (applied.TryGetValue("Head", out var headMarkings)
                && headMarkings.TryGetValue(HumanoidVisualLayers.Snout, out var snoutMarkings)
                && snoutMarkings.Count > 0)
            {
                _visualBody.ApplyMarkings(ent.Owner, new()
                {
                    ["Head"] = new()
                    {
                        [HumanoidVisualLayers.Snout] = new List<Marking>() { new(msg.State, snoutMarkings[0].MarkingColors.Count) },
                    },
                });
            }
            else if (_prototype.TryIndex<MarkingPrototype>(msg.State, out var proto))
            {
                _visualBody.ApplyMarkings(ent.Owner, new()
                {
                    ["Head"] = new()
                    {
                        [HumanoidVisualLayers.Snout] = new List<Marking>() { proto.AsMarking() },
                    },
                });
            }
        }

        ent.Comp.SelectedFace = msg.State;
        Dirty(ent);
        _ui.CloseUi(ent.Owner, IpcFaceUiKey.Face);
    }
    private void OnEmpPulse(EntityUid uid, IpcComponent component, ref EmpPulseEvent args)
    {
        args.Affected = true;

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Shock", 30);
        _damageable.TryChangeDamage(uid, damage);
    }
    private void OnMobStateChanged(EntityUid uid, IpcComponent component, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
        {
            var sound = EnsureComp<SpamEmitSoundComponent>(uid);
            sound.Sound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
            sound.MinInterval = TimeSpan.FromSeconds(15);
            sound.MaxInterval = TimeSpan.FromSeconds(30);
            sound.PopUp = Loc.GetString("sleep-ipc");
            Dirty(uid, sound);
        }
        else
        {
            RemComp<SpamEmitSoundComponent>(uid);
        }
    }
}

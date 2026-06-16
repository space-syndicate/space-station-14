using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Malfunction;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map.Components;

namespace Content.Server.Silicons.Malfunction;

/// <summary>
/// Server-side logic for the Malfunction AI antagonist: regenerates processing power,
/// grants malf actions, and handles all malf ability events (APC hack, machine overload,
/// station blackout, and Doomsday device arming/detonation).
/// </summary>
public sealed partial class MalfunctionAiSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ApcSystem _apc = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfunctionAiComponent, ComponentInit>(OnMalfInit);
        SubscribeLocalEvent<MalfunctionAiComponent, ComponentShutdown>(OnMalfShutdown);

        SubscribeLocalEvent<MalfunctionAiComponent, MalfHackApcEvent>(OnHackApc);
        SubscribeLocalEvent<MalfunctionAiComponent, MalfOverloadMachineEvent>(OnOverloadMachine);
        SubscribeLocalEvent<MalfunctionAiComponent, MalfBlackoutEvent>(OnBlackout);
        SubscribeLocalEvent<MalfunctionAiComponent, MalfDoomsdayEvent>(OnDoomsday);
    }

    private void OnMalfInit(Entity<MalfunctionAiComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ActionEntities.Clear();
        foreach (var proto in ent.Comp.Actions)
        {
            if (_actions.AddAction(ent.Owner, proto) is { } actionUid)
                ent.Comp.ActionEntities.Add(actionUid);
        }
    }

    private void OnMalfShutdown(Entity<MalfunctionAiComponent> ent, ref ComponentShutdown args)
    {
        foreach (var actionUid in ent.Comp.ActionEntities)
        {
            _actions.RemoveAction(ent.Owner, actionUid);
        }
        ent.Comp.ActionEntities.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MalfunctionAiComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Regen processing power.
            if (comp.ProcessingPower < comp.MaxProcessingPower)
            {
                comp.Accumulator += frameTime;
                if (comp.Accumulator >= 1f)
                {
                    var ticks = (int) comp.Accumulator;
                    comp.Accumulator -= ticks;
                    comp.ProcessingPower = FixedPoint2.Min(
                        comp.MaxProcessingPower,
                        comp.ProcessingPower + comp.PowerPerSecond * ticks);
                    Dirty(uid, comp);
                }
            }
        }
    }

    private bool TrySpendPower(Entity<MalfunctionAiComponent> ent, FixedPoint2 cost)
    {
        if (ent.Comp.ProcessingPower < cost)
        {
            _popups.PopupEntity(Loc.GetString("malfunction-ai-popup-not-enough-power"), ent.Owner, ent.Owner);
            return false;
        }

        ent.Comp.ProcessingPower -= cost;
        Dirty(ent);
        return true;
    }

    private void OnHackApc(Entity<MalfunctionAiComponent> ent, ref MalfHackApcEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ApcComponent>(args.Target, out var apc))
        {
            _popups.PopupEntity(Loc.GetString("malfunction-ai-popup-invalid-target"), ent.Owner, ent.Owner);
            return;
        }

        if (!TrySpendPower(ent, ent.Comp.HackApcCost))
            return;

        // Turn off the breaker if it's currently on.
        if (apc.MainBreakerEnabled)
            _apc.ApcToggleBreaker(args.Target, apc, user: ent.Owner);

        // Drain a portion of the battery.
        if (TryComp<BatteryComponent>(args.Target, out var battery))
        {
            var drain = battery.MaxCharge * ent.Comp.HackApcDrainFraction;
            _battery.UseCharge((args.Target, battery), drain);
        }

        _popups.PopupEntity(Loc.GetString("malfunction-ai-popup-hack-apc-success"), ent.Owner, ent.Owner);
        args.Handled = true;
    }

    private void OnOverloadMachine(Entity<MalfunctionAiComponent> ent, ref MalfOverloadMachineEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == ent.Owner)
            return;

        if (!TrySpendPower(ent, ent.Comp.OverloadMachineCost))
            return;

        _explosion.QueueExplosion(
            args.Target,
            "Default",
            ent.Comp.OverloadIntensity,
            5f,
            ent.Comp.OverloadMaxTileIntensity,
            canCreateVacuum: false,
            user: ent.Owner);

        _popups.PopupEntity(Loc.GetString("malfunction-ai-popup-overload-success"), ent.Owner, ent.Owner);
        args.Handled = true;
    }

    private void OnBlackout(Entity<MalfunctionAiComponent> ent, ref MalfBlackoutEvent args)
    {
        if (args.Handled)
            return;

        if (!TrySpendPower(ent, ent.Comp.BlackoutCost))
            return;

        var gridUid = Transform(ent.Owner).GridUid;
        var count = 0;

        var query = EntityQueryEnumerator<ApcComponent, TransformComponent>();
        while (query.MoveNext(out var apcUid, out var apc, out var xform))
        {
            // Restrict to APCs on the same grid as the AI core (or its current map).
            if (gridUid != null && xform.GridUid != gridUid)
                continue;

            if (!apc.MainBreakerEnabled)
                continue;

            _apc.ApcToggleBreaker(apcUid, apc, user: ent.Owner);
            count++;
        }

        var station = _station.GetOwningStation(ent.Owner);
        if (station != null)
        {
            _chat.DispatchStationAnnouncement(
                station.Value,
                Loc.GetString("malfunction-ai-announcement-blackout"),
                Loc.GetString("malfunction-ai-announcement-sender"),
                playDefaultSound: true,
                colorOverride: Color.Red);
        }

        _popups.PopupEntity(Loc.GetString("malfunction-ai-popup-blackout-success", ("count", count)), ent.Owner, ent.Owner);
        args.Handled = true;
    }

    private void OnDoomsday(Entity<MalfunctionAiComponent> ent, ref MalfDoomsdayEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.DoomsdayUsed)
        {
            _popups.PopupEntity(Loc.GetString("malfunction-ai-popup-doomsday-already-used"), ent.Owner, ent.Owner);
            return;
        }

        if (!TrySpendPower(ent, ent.Comp.DoomsdayCost))
            return;

        ent.Comp.DoomsdayUsed = true;
        Dirty(ent);

        // Forwarded to the rule system to start the countdown / handle the win condition.
        var doomEv = new MalfDoomsdayArmedEvent(ent.Owner);
        RaiseLocalEvent(ref doomEv);
        args.Handled = true;
    }
}

/// <summary>
/// Raised broadcast when a Malfunction AI arms the Doomsday device.
/// The Malfunction AI game rule listens for this and starts the countdown / blast.
/// </summary>
[ByRefEvent]
public readonly record struct MalfDoomsdayArmedEvent(EntityUid Ai);

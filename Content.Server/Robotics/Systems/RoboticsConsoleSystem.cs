using Content.Server.Administration.Logs;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.Robotics;
using Content.Shared.Robotics.Components;
using Content.Shared.Robotics.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Server.Research.Systems;

/// <summary>
/// Handles UI and state receiving for the robotics control console.
/// <c>BorgTransponderComponent<c/> broadcasts state from the station's borgs to consoles.
/// </summary>
public sealed class RoboticsConsoleSystem : SharedRoboticsConsoleSystem
{
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;

    // almost never timing out more than 1 per tick so initialize with that capacity
    private List<string> _removing = new(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoboticsConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        // Corvax-Next-MutableLaws-Start
        SubscribeLocalEvent<RoboticsConsoleComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<RoboticsConsoleComponent, EntRemovedFromContainerMessage>(OnRemoved);
        // Corvax-Next-MutableLaws-End
        Subs.BuiEvents<RoboticsConsoleComponent>(RoboticsConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<RoboticsConsoleChangeLawsMessage>(OnChangeLaws); // Corvax-Next-MutableLaws
            subs.Event<RoboticsConsoleDisableMessage>(OnDisable);
            subs.Event<RoboticsConsoleDestroyMessage>(OnDestroy);
            // TODO: camera stuff
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RoboticsConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // remove cyborgs that havent pinged in a while
            _removing.Clear();
            foreach (var (address, data) in comp.Cyborgs)
            {
                if (now >= data.Timeout)
                    _removing.Add(address);
            }

            // needed to prevent modifying while iterating it
            foreach (var address in _removing)
            {
                comp.Cyborgs.Remove(address);
            }

            if (_removing.Count > 0)
                UpdateUserInterface((uid, comp));
        }
    }

    private void OnPacketReceived(Entity<RoboticsConsoleComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        var payload = args.Data;
        if (!payload.TryGetValue(DeviceNetworkConstants.Command, out string? command))
            return;
        if (command != DeviceNetworkConstants.CmdUpdatedState)
            return;

        if (!payload.TryGetValue(RoboticsConsoleConstants.NET_CYBORG_DATA, out CyborgControlData? data))
            return;

        var real = data.Value;
        real.Timeout = _timing.CurTime + ent.Comp.Timeout;
        ent.Comp.Cyborgs[args.SenderAddress] = real;

        UpdateUserInterface(ent);
    }

    // Corvax-Next-MutableLaws-Start
    private void OnInserted(Entity<RoboticsConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUserInterface(ent);
    }

    private void OnRemoved(Entity<RoboticsConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUserInterface(ent);
    }
    // Corvax-Next-MutableLaws-End

    private void OnOpened(Entity<RoboticsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent);
    }

    // Corvax-Next-MutableLaws-Start
    private void OnChangeLaws(Entity<RoboticsConsoleComponent> ent, ref RoboticsConsoleChangeLawsMessage args)
    {
        if (_lock.IsLocked(ent.Owner))
            return;

        if (!ent.Comp.Cyborgs.TryGetValue(args.Address, out var data))
            return;

        if (!_slots.TryGetSlot(ent, ent.Comp.CircuitBoardItemSlot, out var slot) || slot.Item is null)
            return;

        var payload = new NetworkPayload()
        {
            [DeviceNetworkConstants.Command] = RoboticsConsoleConstants.NET_CHANGE_LAWS_COMMAND,
            [RoboticsConsoleConstants.NET_CIRCUIT_BOARD] = slot.Item.Value,
        };

        _deviceNetwork.QueuePacket(ent, args.Address, payload);

        var message = Loc.GetString(ent.Comp.ChangeLawsMessage, ("name", data.Name));
        _radio.SendRadioMessage(ent, message, ent.Comp.RadioChannel, ent);
        _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(args.Actor):user} changed laws of borg {data.Name} with address {args.Address}");
    }
    // Corvax-Next-MutableLaws-End

    private void OnDisable(Entity<RoboticsConsoleComponent> ent, ref RoboticsConsoleDisableMessage args)
    {
        if (_lock.IsLocked(ent.Owner))
            return;

        if (!ent.Comp.Cyborgs.TryGetValue(args.Address, out var data))
            return;

        var payload = new NetworkPayload()
        {
            [DeviceNetworkConstants.Command] = RoboticsConsoleConstants.NET_DISABLE_COMMAND
        };

        _deviceNetwork.QueuePacket(ent, args.Address, payload);
        _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(args.Actor):user} disabled borg {data.Name} with address {args.Address}");
    }

    private void OnDestroy(Entity<RoboticsConsoleComponent> ent, ref RoboticsConsoleDestroyMessage args)
    {
        if (_lock.IsLocked(ent.Owner))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextDestroy)
            return;

        if (!ent.Comp.Cyborgs.Remove(args.Address, out var data))
            return;

        var payload = new NetworkPayload()
        {
            [DeviceNetworkConstants.Command] = RoboticsConsoleConstants.NET_DESTROY_COMMAND
        };

        _deviceNetwork.QueuePacket(ent, args.Address, payload);

        var message = Loc.GetString(ent.Comp.DestroyMessage, ("name", data.Name));
        _radio.SendRadioMessage(ent, message, ent.Comp.RadioChannel, ent);
        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(args.Actor):user} destroyed borg {data.Name} with address {args.Address}");

        ent.Comp.NextDestroy = now + ent.Comp.DestroyCooldown;
        Dirty(ent, ent.Comp);
    }

    private void UpdateUserInterface(Entity<RoboticsConsoleComponent> ent)
    {
        var state = new RoboticsConsoleState(ent.Comp.Cyborgs, _slots.TryGetSlot(ent, ent.Comp.CircuitBoardItemSlot, out var slot) && slot.HasItem); // Corvax-Next-MutableLaws
        _ui.SetUiState(ent.Owner, RoboticsConsoleUiKey.Key, state);
    }
}

using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Icarus;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Icarus;

/// <summary>
/// Handle Icarus activation terminal
/// </summary>
public sealed class IcarusTerminalSystem : EntitySystem
{
    private const string IcarusBeamPrototypeId = "IcarusBeam";

    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQuery<IcarusTerminalComponent>();
        foreach (var terminal in query)
        {
            switch (terminal.Status)
            {
                case IcarusTerminalStatus.FIRE_PREPARING:
                    TickTimer(terminal, frameTime);
                    break;
                case IcarusTerminalStatus.COOLDOWN:
                    TickCooldown(terminal, frameTime);
                    break;
            }
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IcarusTerminalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<IcarusTerminalComponent, ItemSlotChangedEvent>(OnItemSlotChanged);
        SubscribeLocalEvent<IcarusTerminalComponent, GetVerbsEvent<AlternativeVerb>>(AddFireVerb);
    }

    private void OnInit(EntityUid uid, IcarusTerminalComponent component, ComponentInit args)
    {
        component.RemainingTime = component.Timer;
        UpdateStatus(component);
    }

    private void OnItemSlotChanged(EntityUid uid, IcarusTerminalComponent component, ref ItemSlotChangedEvent args)
    {
        UpdateStatus(component);
    }

    private void AddFireVerb(EntityUid uid, IcarusTerminalComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        AlternativeVerb verb = new();
        verb.Act = () => Fire(component);
        verb.Text = Loc.GetString("goldeneye-ui-fire");
        verb.Disabled = !CanFire(uid, component);
        verb.Priority = -1;
        args.Verbs.Add(verb);
    }

    private void Fire(IcarusTerminalComponent component)
    {
        if (component.Status == IcarusTerminalStatus.FIRE_PREPARING)
            return;

        component.RemainingTime = component.Timer;
        component.Status = IcarusTerminalStatus.FIRE_PREPARING;

        _chatSystem.DispatchStationAnnouncement(component.Owner, Loc.GetString("goldeneye-icarus-announcement"),
            Loc.GetString("goldeneye-announce-sender"), false, Color.Red);
        SoundSystem.Play(component.AlertSound.GetSound(), Filter.Broadcast());
    }

    private void UpdateStatus(IcarusTerminalComponent component)
    {
        switch (component.Status)
        {
            case IcarusTerminalStatus.AWAIT_DISKS:
                if (IsAccessGranted(component.Owner))
                    Authorize(component);
                break;
            case IcarusTerminalStatus.FIRE_READY:
            {
                if (!IsAccessGranted(component.Owner))
                {
                    component.Status = IcarusTerminalStatus.AWAIT_DISKS;
                }
                break;
            }
        }
    }

    private bool IsAccessGranted(EntityUid uid)
    {
        return Comp<ItemSlotsComponent>(uid).Slots.Values.All(v => v.HasItem);
    }

    private bool CanFire(EntityUid uid, IcarusTerminalComponent component)
    {
        return IsAccessGranted(uid) &&
               component.Status == IcarusTerminalStatus.FIRE_READY;
    }

    private void Authorize(IcarusTerminalComponent component)
    {
        component.Status = IcarusTerminalStatus.FIRE_READY;

        SoundSystem.Play(component.AccessGrantedSound.GetSound(), Filter.Pvs(component.Owner), component.Owner);

        if (!component.AuthorizationNotified)
        {
            _chatSystem.DispatchStationAnnouncement(component.Owner, Loc.GetString("goldeneye-authorized-announcement"),
                playDefaultSound: false); // TODO: Just pass custom sound path after PR accepting
            SoundSystem.Play("/Audio/Misc/notice1.ogg",
                Filter.Broadcast());
            component.AuthorizationNotified = true;
        }
    }

    private void TickCooldown(IcarusTerminalComponent component, float frameTime)
    {
        component.CooldownTime -= frameTime;
        if (component.CooldownTime <= 0)
        {
            component.CooldownTime = 0;
            component.Status = IcarusTerminalStatus.AWAIT_DISKS;
            UpdateStatus(component);
        }
    }

    private void TickTimer(IcarusTerminalComponent component, float frameTime)
    {
        component.RemainingTime -= frameTime;
        if (component.RemainingTime <= 0)
        {
            component.RemainingTime = 0;
            ActivateBeam(component);
        }
    }

    private void ActivateBeam(IcarusTerminalComponent component)
    {
        component.Status = IcarusTerminalStatus.COOLDOWN;
        component.CooldownTime = component.Cooldown;

        var stationUid = _stationSystem.GetOwningStation(component.Owner);
        if (stationUid == null)
            return;

        SoundSystem.Play(component.FireSound.GetSound(), Filter.Broadcast());
        var gridUids = Comp<StationDataComponent>(stationUid.Value).Grids;
        var coords = Comp<TransformComponent>(gridUids.First()).Coordinates; // TODO: More smart main station grid determine OR replace with radar system
        Spawn(IcarusBeamPrototypeId, coords);
    }
}

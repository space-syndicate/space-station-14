using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Icarus;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Icarus;

/// <summary>
/// Handle Icarus activation terminal
/// </summary>
public sealed class IcarusTerminalSystem : EntitySystem
{
    private const string IcarusBeamPrototypeId = "ImmovableRodSlow";

    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<IcarusTerminalComponent, ItemSlotChangedEvent>(OnItemSlotChanged);
        SubscribeLocalEvent<IcarusTerminalComponent, GetVerbsEvent<AlternativeVerb>>(AddFireVerb);
    }

    private void OnItemSlotChanged(EntityUid uid, IcarusTerminalComponent component, ref ItemSlotChangedEvent args)
    {
        if (IsUnlocked(uid))
        {
            SoundSystem.Play("/Audio/Machines/Nuke/confirm_beep.ogg", Filter.Pvs(uid), uid);

            _chatSystem.DispatchStationAnnouncement(uid, Loc.GetString("goldeneye-authorized-announcement"), playDefaultSound: false);
            SoundSystem.Play("/Audio/Misc/notice1.ogg", Filter.Broadcast()); // TODO: Just pass custom sound path after PR accepting
        }
    }

    private void AddFireVerb(EntityUid uid, IcarusTerminalComponent itemSlots, GetVerbsEvent<AlternativeVerb> args)
    {
        AlternativeVerb verb = new();
        verb.Act = () => Fire(uid);
        verb.Text = Loc.GetString("goldeneye-ui-fire");
        verb.Priority = 0;
        args.Verbs.Add(verb);
    }

    public void Fire(EntityUid uid)
    {
        var stationUid = _stationSystem.GetOwningStation(uid);
        if (stationUid == null)
            return;

        _chatSystem.DispatchStationAnnouncement(uid, Loc.GetString("goldeneye-icarus-announcement"), Loc.GetString("goldeneye-announce-sender"), false, Color.Red);
        SoundSystem.Play("/Audio/Corvax/AssaultOperatives/icarus_alarm.ogg", Filter.Broadcast());

        var gridUids = Comp<StationDataComponent>(stationUid.Value).Grids;
        var coords = Comp<TransformComponent>(gridUids.First()).Coordinates; // TODO: More smart main station grid determine OR replace with radar system
        EntityManager.SpawnEntity(IcarusBeamPrototypeId, coords);
    }

    public bool IsUnlocked(EntityUid uid)
    {
        return EntityManager.GetComponent<ItemSlotsComponent>(uid).Slots.Values.All(v => v.HasItem);
    }
}

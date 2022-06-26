using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Icarus;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Icarus;

/// <summary>
/// Handle Icarus activation terminal
/// </summary>
public sealed class IcarusTerminalSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<IcarusTerminalComponent, ItemSlotChangedEvent>(OnItemSlotChanged);
    }

    private void OnItemSlotChanged(EntityUid uid, IcarusTerminalComponent component, ref ItemSlotChangedEvent args)
    {
        if (IsUnlocked(uid))
        {
            Fire(uid);
        }
    }

    public void Fire(EntityUid uid)
    {
        _chatSystem.DispatchStationAnnouncement(uid, "U AL' DED", "TODO", false, Color.Red);
        SoundSystem.Play("/Audio/Corvax/AssaultOperatives/icarus_alarm.ogg", Filter.Broadcast());

        // TODO: Fire Icarus beam
    }

    public bool IsUnlocked(EntityUid uid)
    {
        return EntityManager.GetComponent<ItemSlotsComponent>(uid).Slots.Values.All(v => v.HasItem);
    }
}

using System.Linq;
using Content.Server.Corvax.Sponsors;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.Loadout;

// NOTE: Full implementation will be in future, now just sponsor items
public sealed class LoadoutSystem : EntitySystem
{
    private const string BackpackSlotId = "back";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly StorageSystem _storageSystem = default!;
    [Dependency] private readonly SponsorsManager _sponsorsManager = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_sponsorsManager.TryGetInfo(ev.Player.UserId, out var sponsor))
        {
            foreach (var loadoutId in sponsor.AllowedLoadout)
            {
                // NOTE: Now is easy to not extract method because event give all info we need
                if (_prototypeManager.TryIndex<LoadoutItemPrototype>(loadoutId, out var loadout))
                {
                    var isSponsorOnly = loadout.SponsorOnly &&
                                        !sponsor.AllowedLoadout.Contains(loadoutId);
                    var isWhitelisted = ev.JobId != null &&
                                        loadout.WhitelistJobs != null &&
                                        !loadout.WhitelistJobs.Contains(ev.JobId);
                    var isBlacklisted = ev.JobId != null &&
                                        loadout.BlacklistJobs != null &&
                                        loadout.BlacklistJobs.Contains(ev.JobId);
                    var isSpeciesRestricted = loadout.SpeciesRestrictions != null &&
                                              loadout.SpeciesRestrictions.Contains(ev.Profile.Species);

                    if (isSponsorOnly || isWhitelisted || isBlacklisted || isSpeciesRestricted)
                        continue;

                    var entity = Spawn(loadout.EntityId, Transform(ev.Mob).Coordinates);
                    if (loadout.SlotId == null) // Take in hand if slot not specified
                    {
                        _handsSystem.TryPickup(ev.Mob, entity);
                        continue;
                    }
                    
                    // Get occupied entity -> Insert to backpack -> Equip loadout gear
                    if (_inventorySystem.TryGetSlotEntity(ev.Mob, loadout.SlotId, out var slotEntity) &&
                        _inventorySystem.TryGetSlotEntity(ev.Mob, BackpackSlotId, out var backEntity) &&
                        _storageSystem.CanInsert(backEntity.Value, slotEntity.Value, out _))
                    {
                        _storageSystem.Insert(backEntity.Value, slotEntity.Value);
                    }
                    _inventorySystem.TryEquip(ev.Mob, entity, loadout.SlotId, true);
                }
            }
        }
    }
}

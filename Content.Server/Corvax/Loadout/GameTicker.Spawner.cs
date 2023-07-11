// using System.Linq;
// using Content.Server.Corvax.Sponsors;
// using Content.Shared.Corvax.Loadout;
// using Content.Shared.Inventory;
// using Content.Shared.Preferences;
// using Robust.Server.Player;
//
// namespace Content.Server.GameTicking;
//
// public sealed partial class GameTicker
// {
//     [Dependency] private readonly InventorySystem _inventory = default!;
//     [Dependency] private readonly SponsorsManager _sponsorsManager = default!;
//
//     private void ApplyLoadout(IPlayerSession player, HumanoidCharacterProfile character)
//     {
//         foreach (var loadoutId in character.LoadoutPreferences)
//         {
//             if (_prototypeManager.TryIndex<LoadoutPrototype>(loadoutId, out var loadout))
//             {
//                 _sponsorsManager.TryGetInfo(player.UserId, out var sponsor);
//
//                 var isSponsorOnly = loadout.SponsorOnly && sponsor != null &&
//                                     !sponsor.AllowedMarkings.Contains(loadoutId);
//                 var isWhitelisted = ev.JobId != null &&
//                                     loadout.WhitelistJobs != null &&
//                                     !loadout.WhitelistJobs.Contains(ev.JobId);
//                 var isBlacklisted = ev.JobId != null &&
//                                     loadout.BlacklistJobs != null &&
//                                     loadout.BlacklistJobs.Contains(ev.JobId);
//                 var isSpeciesRestricted = loadout.SpeciesRestrictions != null &&
//                                           loadout.SpeciesRestrictions.Contains(ev.Profile.Species);
//
//                 if (isSponsorOnly || isWhitelisted || isBlacklisted || isSpeciesRestricted)
//                     continue;
//             }
//         }
//     }
// }

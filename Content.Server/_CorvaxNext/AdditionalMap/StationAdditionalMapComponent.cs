/*
 * All right reserved to CrystallEdge.
 *
 * BUT this file is sublicensed under MIT License
 *
 */

using Robust.Shared.Utility;

namespace Content.Server._CorvaxNext.AdditionalMap;

/// <summary>
/// Loads additional maps from the list at the start of the round.
/// </summary>
[RegisterComponent, Access(typeof(StationAdditionalMapSystem))]
public sealed partial class StationAdditionalMapComponent : Component
{
    /// <summary>
    /// A map paths to load on a new map.
    /// </summary>
    [DataField]
    public List<ResPath> MapPaths = new();
}

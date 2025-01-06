/*
 * All right reserved to CrystallEdge.
 *
 * BUT this file is sublicensed under MIT License
 *
 */

using Content.Server._CorvaxNext.BiomeSpawner.EntitySystems;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.BiomeSpawner.Components;

/// <summary>
/// fills the tile in which it is located with the contents of the biome. Includes: tile, decals and entities
/// </summary>
[RegisterComponent, Access(typeof(BiomeSpawnerSystem))]
public sealed partial class BiomeSpawnerComponent : Component
{
    [DataField]
    public ProtoId<BiomeTemplatePrototype> Biome = "Grasslands";
}

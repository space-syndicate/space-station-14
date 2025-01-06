/*
 * All right reserved to CrystallEdge.
 *
 * BUT this file is sublicensed under MIT License
 *
 */

using System.Linq;
using Content.Server._CorvaxNext.BiomeSpawner.Components;
using System.Numerics;
using Content.Server.Decals;
using Content.Server.GameTicking;
using Content.Server.Parallax;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CorvaxNext.BiomeSpawner.EntitySystems;

public sealed class BiomeSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

	private int _seed = 27;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnRoundStartAttempt);
        SubscribeLocalEvent<BiomeSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnRoundStartAttempt(RoundStartAttemptEvent ev)
    {
        _seed = _random.Next(100000);
    }

    private void OnMapInit(Entity<BiomeSpawnerComponent> ent, ref MapInitEvent args)
    {
        SpawnBiome(ent);
        QueueDel(ent);
    }

    private void SpawnBiome(Entity<BiomeSpawnerComponent> ent)
    {
        var biome = _proto.Index(ent.Comp.Biome);
        var spawnerTransform = Transform(ent);
        if (spawnerTransform.GridUid == null)
            return;
        var gridUid = spawnerTransform.GridUid.Value;
        if (!TryComp<MapGridComponent>(gridUid, out var map))
            return;

        var vec = _transform.GetGridOrMapTilePosition(ent);
        if (!_biome.TryGetTile(vec, biome.Layers, _seed, map, out var tile))
            return;

        // Set new tile
        _maps.SetTile(gridUid, map, vec, tile.Value);
        var tileCenterVec = vec + map.TileSizeHalfVector;

        // Remove old decals
        var oldDecals = _decals.GetDecalsInRange(gridUid, tileCenterVec);
        foreach (var (id, _) in oldDecals)
        {
            _decals.RemoveDecal(gridUid, id);
        }

        //Add decals
        if (_biome.TryGetDecals(vec, biome.Layers, _seed, map, out var decals))
        {
            foreach (var decal in decals)
            {
                _decals.TryAddDecal(decal.ID, new EntityCoordinates(gridUid, decal.Position), out _);
            }
        }

        // Remove entities
        var oldEntities = _lookup.GetEntitiesInRange(spawnerTransform.Coordinates, 0.48f);
        // TODO: Replace this with GetEntitiesInBox2
        foreach (var entToRemove in oldEntities.Concat(new[] { ent.Owner })) // Do not remove self
        {
            QueueDel(entToRemove);
        }

        if (_biome.TryGetEntity(vec, biome.Layers, tile.Value, _seed, map, out var entityProto))
            Spawn(entityProto, new EntityCoordinates(gridUid, tileCenterVec));
    }
}

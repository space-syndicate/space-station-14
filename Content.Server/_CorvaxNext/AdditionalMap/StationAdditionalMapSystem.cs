/*
 * All right reserved to CrystallEdge.
 *
 * BUT this file is sublicensed under MIT License
 *
 */

using Content.Server.Parallax;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Teleportation.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.AdditionalMap;

public sealed partial class StationAdditionalMapSystem : EntitySystem
{
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly LinkedEntitySystem _linkedEntity = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationAdditionalMapComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<StationAdditionalMapComponent> addMap, ref StationPostInitEvent args)
    {
        if (!TryComp(addMap, out StationDataComponent? dataComp))
            return;

        foreach (var path in addMap.Comp.MapPaths)
        {
            var mapUid = _map.CreateMap(out var mapId);
            Log.Info($"Created map {mapId} for StationAdditionalMap system");
            var options = new MapLoadOptions { LoadMap = true };
            if (!_mapLoader.TryLoad(mapId, path.ToString(), out var roots, options))
            {
                Log.Error($"Failed to load map from {path}!");
                Del(mapUid);
                return;
            }
        }
    }
}

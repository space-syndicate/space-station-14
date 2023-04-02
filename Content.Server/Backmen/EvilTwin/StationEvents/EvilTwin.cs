using System.Linq;
using Content.Server.Spawners.Components;
using Content.Server.StationEvents.Events;
using Robust.Shared.Random;

namespace Content.Server.Backmen.EvilTwin.StationEvents;
public sealed class EvilTwin : StationEventSystem
    {
        public override string Prototype => "EvilTwin";

        public override void Started()
        {
            base.Started();
            var lateJoinSpawns = (from s in EntityQuery<SpawnPointComponent, TransformComponent>()
                where s.Item1.SpawnType == SpawnPointType.LateJoin
                select s).ToList<ValueTuple<SpawnPointComponent, TransformComponent>>();
            if (lateJoinSpawns.Count == 0)
            {
                Sawmill.Error("Map not have latejoin spawnpoints for creating evil twin spawner");
                return;
            }
            var coords = _random.Pick(lateJoinSpawns).Item2.Coordinates;
            cloneSpawnPoint = Spawn(SpawnPointPrototype, coords);

        }

        public override void Ended()
        {
            base.Ended();
            if (cloneSpawnPoint!=null && cloneSpawnPoint.Value.Valid)
            {
                QueueDel(cloneSpawnPoint.Value);
            }
        }

        private EntityUid? cloneSpawnPoint { get; set;}

        [Dependency] private readonly IRobustRandom _random = default!;

        private const string SpawnPointPrototype = "SpawnPointEvilTwin";
    }

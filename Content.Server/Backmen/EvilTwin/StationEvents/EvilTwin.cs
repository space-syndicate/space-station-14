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

            if(_evilTwinSystem.MakeTwin(out var _cloneSpawnPoint)){
                cloneSpawnPoint = _cloneSpawnPoint;
            }else{
                Sawmill.Error("Map not have latejoin spawnpoints for creating evil twin spawner");
            }
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

        [Dependency] private readonly EvilTwinSystem _evilTwinSystem = default!;
    }

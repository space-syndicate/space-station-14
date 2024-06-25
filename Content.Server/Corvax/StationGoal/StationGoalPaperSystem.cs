using System.Linq;
using Content.Server.Fax;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Corvax.StationGoal
{
    /// <summary>
    ///     System to spawn paper with station goal.
    /// </summary>
    public sealed class StationGoalPaperSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly FaxSystem _fax = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly StationSystem _station = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StationGoalComponent ,MapInitEvent>(OnMapInit);
        }

        private void OnMapInit(Entity<StationGoalComponent> station, ref MapInitEvent args)
        {
            var playerCount = _playerManager.PlayerCount;

            StationGoalPrototype? selGoal = null;
            while (station.Comp.Goals.Count > 0)
            {
                var goalId = _random.Pick(station.Comp.Goals);
                var goalProto = _proto.Index(goalId);

                if (playerCount > goalProto.MaxPlayers ||
                    playerCount < goalProto.MinPlayers)
                {
                    station.Comp.Goals.Remove(goalId);
                    continue;
                }

                selGoal = goalProto;
            }

            if (selGoal is null)
                return;

            if (SendStationGoal(station, selGoal))
            {
                Log.Info($"Goal {selGoal.ID} has been sent to station {MetaData(station).EntityName}");
            }
        }

        public bool SendStationGoal(EntityUid? ent, ProtoId<StationGoalPrototype> goal)
        {
            return SendStationGoal(ent, _proto.Index(goal));
        }

        /// <summary>
        ///     Send a station goal to all faxes which are authorized to receive it.
        /// </summary>
        /// <returns>True if at least one fax received paper</returns>
        public bool SendStationGoal(EntityUid? ent, StationGoalPrototype goal)
        {
            if (ent is null)
                return false;

            if (!TryComp<StationDataComponent>(ent, out var stationData))
                return false;

            var printout = new FaxPrintout(
                Loc.GetString(goal.Text, ("station", MetaData(ent.Value).EntityName)),
                Loc.GetString("station-goal-fax-paper-name"),
                null,
                null,
                "paper_stamp-centcom",
                new List<StampDisplayInfo>
                {
                    new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#006600") },
                });

            var wasSent = false;
            var query = EntityQueryEnumerator<FaxMachineComponent>();
            while (query.MoveNext(out var uid, out var fax))
            {
                if (!fax.ReceiveStationGoal)
                    continue;

                var largestGrid = _station.GetLargestGrid(stationData);
                var grid = Transform(uid).GridUid;
                if (grid is not null && largestGrid == grid.Value)
                {
                    _fax.Receive(uid, printout, null, fax);
                    wasSent = true;
                }
            }
            return wasSent;
        }
    }
}

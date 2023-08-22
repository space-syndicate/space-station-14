using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Fax;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Corvax.StationGoal
{
    /// <summary>
    ///     System to spawn paper with station goal.
    /// </summary>
    public sealed class StationGoalPaperSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly FaxSystem _faxSystem = default!;
        [Dependency] private readonly StationSystem _station = default!;

        private static readonly Regex StationIdRegex = new(@".*-(\d+)$");

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        }

        private void OnRoundStarted(RoundStartedEvent ev)
        {
            SendRandomGoal();
        }

        public bool SendRandomGoal()
        {
            var availableGoals = _prototypeManager.EnumeratePrototypes<StationGoalPrototype>().ToList();
            var goal = _random.Pick(availableGoals);
            return SendStationGoal(goal);
        }

        /// <summary>
        ///     Send a station goal to all faxes which are authorized to receive it.
        /// </summary>
        /// <returns>True if at least one fax received paper</returns>
        public bool SendStationGoal(StationGoalPrototype goal)
        {
            var enumerator = EntityManager.EntityQueryEnumerator<FaxMachineComponent>();
            var wasSent = false;
            while (enumerator.MoveNext(out var uid, out var fax))
            {
                if (!fax.ReceiveStationGoal) continue;

                if (!TryComp<MetaDataComponent>(_station.GetOwningStation(uid), out var meta))
                    continue;

                var stationId = StationIdRegex.Match(meta.EntityName).Groups[1].Value;

                var printout = new FaxPrintout(
                    Loc.GetString(goal.Text,
                        ("date", DateTime.Now.AddYears(1000).ToString("dd.MM.yyyy")),
                        ("station", string.IsNullOrEmpty(stationId) ? "???" : stationId)),
                    Loc.GetString("station-goal-fax-paper-name"),
                    null,
                    "paper_stamp-centcom",
                    new List<StampDisplayInfo>
                    {
                        new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#BB3232") },
                    });
                _faxSystem.Receive(uid, printout, null, fax);

                wasSent = true;
            }

            return wasSent;
        }
    }
}

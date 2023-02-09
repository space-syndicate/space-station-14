using System.Linq;
using Content.Server.Fax;
using Content.Server.Objectives;
using Content.Shared.GameTicking;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
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
            var chance = _random.NextFloat(0.0f, 1.0f);


            if (chance > 0.3f)
            {
                var newgoal = GetRandomModularGoal("ModularGoalsGroups");
                if (newgoal != null)
                    return SendStationGoal(newgoal);
            }

            var availableGoals = _prototypeManager.EnumeratePrototypes<StationGoalPrototype>().ToList();
            var goal = _random.Pick(availableGoals);
            return SendStationGoal(goal!);

        }

        /// <summary>
        ///     Send a station goal to all faxes which are authorized to receive it.
        /// </summary>
        /// <returns>True if at least one fax received paper</returns>
        public bool SendStationGoal(StationGoalPrototype goal)
        {
            var faxes = EntityManager.EntityQuery<FaxMachineComponent>();
            var wasSent = false;
            foreach (var fax in faxes)
            {
                if (!fax.ReceiveStationGoal) continue;

                var printout = new FaxPrintout(
                    Loc.GetString(goal.Text),
                    Loc.GetString("station-goal-fax-paper-name"),
                    null,
                    "paper_stamp-cent",
                    new() { Loc.GetString("stamp-component-stamped-name-centcom") });
                _faxSystem.Receive(fax.Owner, printout, null, fax);

                wasSent = true;
            }

            return wasSent;
        }
        public StationGoalPrototype? GetRandomModularGoal(string modGoalsGroupProto)
        {
            if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(modGoalsGroupProto, out var groups))
            {
                Logger.Error("Tried to get a random objective, but can't index WeightedRandomPrototype " + modGoalsGroupProto);
                return null;
            }

            var goals = new List<StationGoalModularPrototype>();

            Int32 goalsNum = 3;



            for (Int32 i = 0; i < goalsNum; i++)
            {
                if (groups.Weights.Count < 1)
                    break;

                var groupId = groups.Pick(_random);
                if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(groupId, out var group))
                {
                    continue;
                }

                var goalid = group.Pick(_random);
                if (!_prototypeManager.TryIndex<StationGoalModularPrototype>(goalid, out var goal))
                {
                    continue;
                }
                if (goal != null)
                {
                    goals.Add(goal);
                    groups.Weights.Remove(groupId);
                }
            }
            if (goals.Count < 2)
                return null;

            String goalString = String.Empty;
            foreach (var goal in goals)
            {
                goalString += "\n" + Loc.GetString(goal.Text);
            }
            if (goalString == String.Empty)
                return null;

            var mainGoal = new StationGoalPrototype();
            mainGoal.Text = Loc.GetString("station-goal-modular");
            mainGoal.Text += goalString;

            return mainGoal;



        }



    }
}

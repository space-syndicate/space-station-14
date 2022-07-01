using Content.Server.Communications;
using Content.Server.Paper;

namespace Content.Server.Corvax.StationGoal
{
    /// <summary>
    ///     System to spawn paper with station goal.
    /// </summary>
    public sealed class StationGoalPaperSystem : EntitySystem
    {
        public void SpawnStationGoalPaper(StationGoalPrototype goal)
        {
            var consoles = EntityManager.EntityQuery<CommunicationsConsoleComponent>();
            foreach (var console in consoles)
            {
                if (!EntityManager.TryGetComponent((console).Owner, out TransformComponent? transform))
                    continue;

                var consolePos = transform.MapPosition;
                var paperId = EntityManager.SpawnEntity("StationGoalPaper", consolePos);
                var paper = EntityManager.GetComponent<PaperComponent>(paperId);

                paper.Content = Loc.GetString(goal.Text);
            }
        }
    }
}

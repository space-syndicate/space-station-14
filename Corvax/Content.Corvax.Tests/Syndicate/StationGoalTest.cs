using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Corvax.StationGoal;
using Content.Shared.MassMedia.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Corvax.Tests.Syndicate;

[TestFixture]
public sealed class StationGoalTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        Map = PoolManager.TestStation,
    };

    [Test]
    public async Task ValidateGoals()
    {
        var pair = Pair;
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var goals = protoManager.EnumeratePrototypes<StationGoalPrototype>().ToList();
            Assert.That(goals, Is.Not.Empty, "There must be at least one Station Goal.");

            var seen = new HashSet<string>();
            foreach (var goal in goals)
            {
                Assert.That(goal.Text, Is.Not.Null.And.Not.Empty, $"Station Goal {goal.ID} has no text.");
                Assert.That(seen.Add(goal.ID), Is.True, $"Duplicate Station Goal ID: {goal.ID}");
            }
        });
    }

    /// <summary>
    /// Sends a station goal to a fax on the pre-loaded station and verifies the result.
    /// If <paramref name="expectNews"/> is set, also checks whether a news article is published.
    /// </summary>
    [TestCase(false, TestName = "SendsGoalToFaxWithoutNews")]
    [TestCase(true,  TestName = "SendsGoalToFaxWithNews")]
    public async Task SendsGoalToFax(bool expectNews)
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var goalSystem = server.System<StationGoalPaperSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        EntityUid station = default;
        int articlesBefore = 0;

        await server.WaitPost(() =>
        {
            var stationQuery = entManager.EntityQueryEnumerator<StationDataComponent>();
            Assert.That(stationQuery.MoveNext(out var stationUid, out _), "Pool did not create a station from the map.");
            station = stationUid;

            if (expectNews)
            {
                // Ensure the component exists so news can be published.
                var stationNews = entManager.EnsureComponent<StationNewsComponent>(station);
                articlesBefore = stationNews.Articles.Count;
            }
            else
            {
                // Remove the component (news writer may have created it at map init)
                // so that the publish step is silently skipped.
                entManager.RemoveComponent<StationNewsComponent>(station);
                articlesBefore = 0;
            }
        });

        var goalId = protoManager.EnumeratePrototypes<StationGoalPrototype>().First().ID;

        await server.WaitPost(() =>
        {
            var result = goalSystem.SendStationGoal(station, goalId);
            Assert.That(result, Is.True, $"SendStationGoal returned false for {goalId} (fax did not accept?)");
        });

        server.RunTicks(2);

        await server.WaitPost(() =>
        {
            var hasNews = entManager.TryGetComponent<StationNewsComponent>(station, out var stationNews);

            if (expectNews)
            {
                Assert.That(hasNews, Is.True, "StationNewsComponent is missing after sending the station goal.");
                Assert.That(stationNews!.Articles.Count, Is.EqualTo(articlesBefore + 1),
                    "Expected exactly one new news article after sending the station goal.");

                var article = stationNews.Articles[^1];
                Assert.That(article.Title, Is.Not.Null.And.Not.Empty, "News article has an empty title.");
                Assert.That(article.Content, Is.Not.Null.And.Not.Empty, "News article has empty content.");
            }
            else
            {
                Assert.That(hasNews, Is.False, "StationNewsComponent should not have been created when news publishing is disabled.");
            }
        });
    }
}

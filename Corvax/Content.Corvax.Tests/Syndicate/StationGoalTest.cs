using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Corvax.StationGoal;
using Content.Shared.Fax.Components;
using Content.Shared.MassMedia.Components;
using Content.Shared.Station.Components;
using Content.Shared.Station.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
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
        var stationSystem = server.System<StationSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        EntityUid station = default;
        int articlesBefore = 0;

        await server.WaitPost(() =>
        {
            var stationQuery = entManager.EntityQueryEnumerator<StationDataComponent>();
            Assert.That(stationQuery.MoveNext(out var stationUid, out _), "Pool did not create a station from the map.");
            station = stationUid;

            // The test map is bare-bones and has no fax machines, so spawn one
            // that is authorized to receive station goals.
            SpawnGoalFax(entManager, stationSystem, station);

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

    /// <summary>
    /// Inverse of <see cref="SendsGoalToFax"/>: strips every fax machine from the
    /// pre-loaded station and verifies that sending a station goal fails instead
    /// of silently succeeding.
    /// </summary>
    [Test]
    public async Task SendsGoalToFaxWithoutAnyFaxMachines()
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var goalSystem = server.System<StationGoalPaperSystem>();
        var stationSystem = server.System<StationSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        EntityUid station = default;

        await server.WaitPost(() =>
        {
            var stationQuery = entManager.EntityQueryEnumerator<StationDataComponent>();
            Assert.That(stationQuery.MoveNext(out var stationUid, out _), "Pool did not create a station from the map.");
            station = stationUid;

            // The test map is bare-bones and has no fax machines, so spawn one
            // first to make sure this test actually proves something.
            var spawnedFax = SpawnGoalFax(entManager, stationSystem, station);
            Assert.That(entManager.HasComponent<FaxMachineComponent>(spawnedFax), Is.True,
                "Sanity: the spawned fax machine must have a FaxMachineComponent.");

            var faxQuery = entManager.EntityQueryEnumerator<FaxMachineComponent>();
            var faxUids = new List<EntityUid>();
            while (faxQuery.MoveNext(out var faxUid, out _))
            {
                faxUids.Add(faxUid);
            }

            Assert.That(faxUids, Is.Not.Empty,
                "Sanity: there must be at least one fax machine, otherwise this test proves nothing.");

            foreach (var faxUid in faxUids)
            {
                entManager.RemoveComponent<FaxMachineComponent>(faxUid);
            }

            // Re-check that the removal actually took effect.
            var remaining = entManager.EntityQueryEnumerator<FaxMachineComponent>();
            Assert.That(remaining.MoveNext(out _, out _), Is.False,
                "Sanity: all fax machines must have been removed before sending the goal.");
        });

        var goalId = protoManager.EnumeratePrototypes<StationGoalPrototype>().First().ID;

        await server.WaitPost(() =>
        {
            var result = goalSystem.SendStationGoal(station, goalId);
            Assert.That(result, Is.False,
                $"SendStationGoal must fail when no fax machine exists on the station, but it returned true for {goalId}.");
        });
    }

    /// <summary>
    /// Spawns a fax machine on the station's grid that is authorized to receive station goals.
    /// </summary>
    private static EntityUid SpawnGoalFax(IEntityManager entManager, StationSystem stationSystem, EntityUid station)
    {
        var grid = stationSystem.GetLargestGrid(station);
        Assert.That(grid, Is.Not.Null, "The station has no grid to spawn the fax machine on.");

        // Use grid-local coordinates so the fax is actually parented to the station's grid.
        // TransformComponent.Coordinates returns map coordinates here, which can leave the fax on the map
        // (null GridUid) and make GetOwningStation() fail, so the goal fax is never accepted.
        var coords = new EntityCoordinates(grid!.Value, 0, 0);
        return entManager.SpawnAttachedTo("FaxMachineCaptain", coords);
    }
}

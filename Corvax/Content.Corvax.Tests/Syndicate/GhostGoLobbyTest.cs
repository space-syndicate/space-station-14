using Content.IntegrationTests.Fixtures;
using Content.Server.Corvax.Ghost;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;

namespace Content.Corvax.Tests.Syndicate;

[TestFixture]
public sealed class GhostGoLobbyTest : GameTest
{
    private const string TestName = "Oleg";

    [Test]
    public async Task MarkCharacterTracksUsedCharacter()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();
        var profile = MakeProfile(TestName);

        await server.WaitPost(() =>
        {
            Assert.That(system.IsCharacterUsed(profile), Is.False, "Character must not be tracked before marking.");

            system.MarkCharacterUsed(profile);

            Assert.That(system.IsCharacterUsed(profile), Is.True, "Character must be tracked after marking.");
        });
    }

    [Test]
    public async Task UntrackedCharacterIsNotReportedAsUsed()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();

        await server.WaitPost(() =>
        {
            var profile = MakeProfile(TestName);

            Assert.That(system.IsCharacterUsed(profile), Is.False, "Untracked character must not be reported as used.");
        });
    }

    [Test]
    public async Task ProfilesWithSameValuesHashEqually()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();

        await server.WaitPost(() =>
        {
            var a = MakeProfile(TestName);
            var b = MakeProfile(TestName);

            system.MarkCharacterUsed(a);

            Assert.That(system.IsCharacterUsed(b), Is.True, "Two profiles with identical Name/Sex/Age/Species must hash equally.");
        });
    }

    [TestCase(TestName, "Ne" + TestName, TestName = "DifferentNameDoesNotMatch")]
    [TestCase(TestName, TestName, TestName = "SameNameMatches")]
    public async Task CharacterMatchingByName(string usedName, string checkName)
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();

        await server.WaitPost(() =>
        {
            system.MarkCharacterUsed(MakeProfile(usedName));

            var expected = usedName == checkName;
            Assert.That(system.IsCharacterUsed(MakeProfile(checkName)), Is.EqualTo(expected),
                expected
                    ? $"Expected '{checkName}' to match the previously used '{usedName}'."
                    : $"Expected '{checkName}' NOT to match the previously used '{usedName}'.");
        });
    }

    [Test]
    public async Task DifferentAgeDoesNotMatch()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();

        await server.WaitPost(() =>
        {
            var used = MakeProfile(TestName, age: 25);
            var other = MakeProfile(TestName, age: 40);

            system.MarkCharacterUsed(used);

            Assert.That(system.IsCharacterUsed(other), Is.False, "Profiles with different Age must hash differently.");
        });
    }

    [Test]
    public async Task DifferentSexDoesNotMatch()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();

        await server.WaitPost(() =>
        {
            var used = MakeProfile(TestName, sex: Sex.Female);
            var other = MakeProfile(TestName, sex: Sex.Male);

            system.MarkCharacterUsed(used);

            Assert.That(system.IsCharacterUsed(other), Is.False, "Profiles with different Sex must hash differently.");
        });
    }

    [Test]
    public async Task DifferentSpeciesDoesNotMatch()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();

        await server.WaitPost(() =>
        {
            var used = MakeProfile(TestName, species: "Human");
            var other = MakeProfile(TestName, species: "Reptilian");

            system.MarkCharacterUsed(used);

            Assert.That(system.IsCharacterUsed(other), Is.False, "Profiles with different Species must hash differently.");
        });
    }

    [Test]
    public async Task PreRoundLobbyClearsUsedCharacters()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();
        var profile = MakeProfile(TestName);

        await server.WaitPost(() =>
        {
            system.MarkCharacterUsed(profile);
            Assert.That(system.IsCharacterUsed(profile), Is.True, "Setup sanity: character must be marked before clearing.");
        });

        await server.WaitPost(() =>
        {
            // Simulate a transition into the pre-round lobby.
            var ev = new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PreRoundLobby);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ev);
        });

        await server.WaitPost(() =>
        {
            Assert.That(system.IsCharacterUsed(profile), Is.False, "Used characters must be cleared when entering the pre-round lobby.");
        });
    }

    [Test]
    public async Task OtherRunLevelsDoNotClearUsedCharacters()
    {
        var server = Pair.Server;
        var system = server.System<GhostGoLobbySystem>();
        var profile = MakeProfile(TestName);

        await server.WaitPost(() =>
        {
            system.MarkCharacterUsed(profile);
        });

        await server.WaitPost(() =>
        {
            // Transition into PostRound — must NOT clear the tracked set.
            var ev = new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PostRound);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ev);
        });

        await server.WaitPost(() =>
        {
            Assert.That(system.IsCharacterUsed(profile), Is.True, "Used characters must only be cleared on PreRoundLobby, not other run levels.");
        });
    }

    /// <summary>
    /// Builds a minimal profile with a fixed Name/Sex/Age/Species combo.
    /// Sex and Age are variadic so each test can tweak a single field
    /// while keeping the rest stable.
    /// </summary>
    private static HumanoidCharacterProfile MakeProfile(
        string name,
        Sex sex = Sex.Male,
        int age = 25,
        string species = "Human")
    {
        return new HumanoidCharacterProfile()
            .WithName(name)
            .WithSex(sex)
            .WithAge(age)
            .WithSpecies(species);
    }
}

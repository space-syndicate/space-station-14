using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Corvax.SecApartment;
using Content.Shared.SecApartment;
using Content.Shared.StatusIcon;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Corvax.Tests.Syndicate;

[TestFixture]
public sealed class SecApartmentDataTest
{
    [Test]
    public void SquadDefaultsAreCorrect()
    {
        var squad = new Squad("squad_1234", "Alpha Team");

        Assert.Multiple(() =>
        {
            Assert.That(squad.SquadId, Is.EqualTo("squad_1234"));
            Assert.That(squad.Name, Is.EqualTo("Alpha Team"));
            Assert.That(squad.Description, Is.Empty, "Description must start empty.");
            Assert.That(squad.Members, Is.Empty, "Members must start empty.");
            Assert.That(squad.Status, Is.EqualTo(SquadStatus.Active), "New squad must be Active.");
            Assert.That(squad.IconId, Is.EqualTo(SquadIconNum.Alpha), "New squad must use Alpha icon.");
        });
    }

    [Test]
    public void CrewMemberInfoPreservesFields()
    {
        var owner = new NetEntity(42);
        var info = new CrewMemberInfo("id1", owner, "John", "Engineer", "JobIconEngineer", null);

        Assert.Multiple(() =>
        {
            Assert.That(info.MemberId, Is.EqualTo("id1"));
            Assert.That(info.OwnerUid, Is.EqualTo(owner));
            Assert.That(info.Name, Is.EqualTo("John"));
            Assert.That(info.JobTitle, Is.EqualTo("Engineer"));
            Assert.That(info.JobIcon, Is.EqualTo("JobIconEngineer"));
            Assert.That(info.SensorStatus, Is.Null);
        });
    }

    [Test]
    public void TimerEntryPreservesFields()
    {
        var uid = new NetEntity(7);
        var entry = new TimerEntry(uid, "Bomb", TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));

        Assert.Multiple(() =>
        {
            Assert.That(entry.TimerUid, Is.EqualTo(uid));
            Assert.That(entry.Label, Is.EqualTo("Bomb"));
            Assert.That(entry.RemainingTime, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(entry.TotalTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(entry.FinishedAt, Is.Null);
        });
    }

    [Test]
    public void StationDataStartsEmpty()
    {
        var data = new StationData();

        Assert.Multiple(() =>
        {
            Assert.That(data.Squads, Is.Empty);
            Assert.That(data.TrackedTimers, Is.Empty);
        });
    }

    [Test]
    public void SquadMemberComponentDefaultsToAlphaIcon()
    {
        var comp = new SquadMemberComponent();
        Assert.That(comp.StatusIcon.Id, Is.EqualTo("SecuritySquadIconAlpha"));
    }

    [Test]
    public void SecApartmentComponentStationIsNullByDefault()
    {
        var comp = new SecApartmentComponent();
        Assert.That(comp.Station, Is.Null);
    }

    [Test]
    public void SquadIconNumHasTwentyFourEntries()
    {
        var values = Enum.GetValues<SquadIconNum>();
        Assert.That(values.Length, Is.EqualTo(24),
            "SquadIconNum must contain exactly 24 entries (Alpha..Omega).");
    }

    [Test]
    public void SquadStatusHasActiveAndOnBreak()
    {
        var values = Enum.GetValues<SquadStatus>().Cast<SquadStatus>().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(values, Does.Contain(SquadStatus.Active));
            Assert.That(values, Does.Contain(SquadStatus.OnBreak));
        });
    }
}

[TestFixture]
public sealed class SecApartmentPrototypeTest : GameTest
{
    /// <summary>
    /// Every icon that <c>SecApartmentSystem.GetIconPrototypeId</c> can return
    /// must exist as a <see cref="StatusIconPrototype"/>. If someone renames
    /// a squad icon prototype without updating the switch, this test catches it.
    /// </summary>
    [Test]
    public async Task AllSquadIconPrototypesExist()
    {
        var server = Pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        var names = new[]
        {
            "SecuritySquadIconAlpha",
            "SecuritySquadIconBeta",
            "SecuritySquadIconGamma",
            "SecuritySquadIconDelta",
            "SecuritySquadIconEpsilon",
            "SecuritySquadIconZeta",
            "SecuritySquadIconHeta",
            "SecuritySquadIconTheta",
            "SecuritySquadIconIota",
            "SecuritySquadIconKappa",
            "SecuritySquadIconLambda",
            "SecuritySquadIconMu",
            "SecuritySquadIconNu",
            "SecuritySquadIconXi",
            "SecuritySquadIconOmicron",
            "SecuritySquadIconPi",
            "SecuritySquadIconRo",
            "SecuritySquadIconSigma",
            "SecuritySquadIconTau",
            "SecuritySquadIconUpsilon",
            "SecuritySquadIconFi",
            "SecuritySquadIconHi",
            "SecuritySquadIconPsi",
            "SecuritySquadIconOmega",
        };

        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var name in names)
                {
                    Assert.That(protoManager.HasIndex<StatusIconPrototype>(name), Is.True,
                        $"Squad icon prototype '{name}' is referenced by GetIconPrototypeId but not registered.");
                }
            });
        });
    }

    [Test]
    public async Task SecuritySquadIconCountMatchesEnum()
    {
        var server = Pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var squadIcons = protoManager.EnumeratePrototypes<StatusIconPrototype>()
                .Where(p => p.ID.StartsWith("SecuritySquadIcon"))
                .ToList();

            var enumCount = Enum.GetValues<SquadIconNum>().Length;

            Assert.That(squadIcons.Count, Is.EqualTo(enumCount),
                $"Expected {enumCount} SecuritySquadIcon* prototypes, found {squadIcons.Count}. " +
                "If you added a new SquadIconNum entry, add the matching prototype.");
        });
    }

    [Test]
    public async Task SecApartmentSystemIsRegistered()
    {
        var server = Pair.Server;

        await server.WaitPost(() =>
        {
            var sys = server.System<Content.Server.Corvax.SecApartment.SecApartmentSystem>();
            Assert.That(sys, Is.Not.Null, "SecApartmentSystem must be registered on the server.");
        });
    }
}

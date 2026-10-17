using Content.Server.Corvax.Documents;
using Content.Shared.Access.Components;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Corvax.Documents;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.Corvax.Tests.Syndicate;

[TestFixture]
public sealed class DocumentPrinterTest : GameTest
{
    [Test]
    public async Task ReplacesAllPlaceholders()
    {
        var pair = Pair;
        var server = pair.Server;

        var docSystem = server.System<DocumentPrinterSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        // Build content using the actual localized variable tokens, so the test
        // doesn't hardcode the "$date" / "$station" / etc. symbol format.
        var dateVar = loc.GetString("doc-var-date");
        var stationVar = loc.GetString("doc-var-station");
        var nameVar = loc.GetString("doc-var-name");
        var jobVar = loc.GetString("doc-var-job");

        var content = $"Date: {dateVar}, Station: {stationVar}, Name: {nameVar}, Job: {jobVar}";

        await server.WaitPost(() =>
        {
            var formatted = docSystem.FormatString(content, "MyStation");

            Assert.Multiple(() =>
            {
                Assert.That(formatted, Does.Not.Contain(dateVar), "Date placeholder was not replaced.");
                Assert.That(formatted, Does.Not.Contain(stationVar), "Station placeholder was not replaced.");
                Assert.That(formatted, Does.Not.Contain(nameVar), "Name placeholder was not replaced.");
                Assert.That(formatted, Does.Not.Contain(jobVar), "Job placeholder was not replaced.");

                Assert.That(formatted, Does.Contain("MyStation"), "Station name was not inserted.");
                Assert.That(formatted, Does.Contain(loc.GetString("doc-text-printer-default-name")),
                    "Default name was not inserted when no ID card is provided.");
                Assert.That(formatted, Does.Contain(loc.GetString("doc-text-printer-default-job")),
                    "Default job was not inserted when no ID card is provided.");
            });
        });
    }

    [Test]
    public async Task UsesIdCardWhenProvided()
    {
        var pair = Pair;
        var server = pair.Server;

        var docSystem = server.System<DocumentPrinterSystem>();
        var entManager = server.ResolveDependency<IEntityManager>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        var nameVar = loc.GetString("doc-var-name");
        var jobVar = loc.GetString("doc-var-job");
        var content = $"Name: {nameVar}, Job: {jobVar}";

        await server.WaitPost(() =>
        {
            var idCardEntity = entManager.SpawnEntity(null, MapCoordinates.Nullspace);
            var idCard = entManager.EnsureComponent<IdCardComponent>(idCardEntity);
            idCard.FullName = "John Doe";
            idCard.LocalizedJobTitle = "Engineer";

            var formatted = docSystem.FormatString(content, "MyStation", idCard);

            Assert.Multiple(() =>
            {
                Assert.That(formatted, Does.Not.Contain(nameVar), "Name placeholder was not replaced with ID card name.");
                Assert.That(formatted, Does.Not.Contain(jobVar), "Job placeholder was not replaced with ID card job.");
                Assert.That(formatted, Does.Contain("John Doe"), "ID card full name was not inserted.");
                Assert.That(formatted, Does.Contain("Engineer"), "ID card job title was not inserted.");
            });

            entManager.DeleteEntity(idCardEntity);
        });
    }

    [Test]
    public async Task UsesDefaultStationWhenNull()
    {
        var pair = Pair;
        var server = pair.Server;

        var docSystem = server.System<DocumentPrinterSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        var stationVar = loc.GetString("doc-var-station");
        var content = $"Station: {stationVar}";

        await server.WaitPost(() =>
        {
            var formatted = docSystem.FormatString(content, null);

            Assert.Multiple(() =>
            {
                Assert.That(formatted, Does.Not.Contain(stationVar), "Station placeholder was not replaced when station is null.");
                Assert.That(formatted, Does.Contain(loc.GetString("doc-text-printer-default-station")),
                    "Default station name was not used when station is null.");
            });
        });
    }

    [Test]
    public async Task HasDefaultSlotName()
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            var ent = entManager.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entManager.EnsureComponent<DocumentPrinterComponent>(ent);

            Assert.That(comp.SlotName, Is.EqualTo("id"), "DocumentPrinterComponent.SlotName should default to \"id\".");

            entManager.DeleteEntity(ent);
        });
    }
}

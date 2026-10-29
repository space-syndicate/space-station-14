using System.IO;
using System.Text;
using System.Text.Json;
using Content.IntegrationTests.Fixtures;
using Content.Server.Corvax.GuideGenerator;

namespace Content.Corvax.Tests.Syndicate.Guidebook;

[TestFixture]
public sealed class GuideGeneratorTest : GameTest
{
    private static string RunGenerator(Action<Stream> publish)
    {
        using var ms = new MemoryStream();
        publish(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test]
    public async Task ComponentListJsonIsValid()
    {
        var server = Pair.Server;

        await server.WaitPost(() =>
        {
            var text = RunGenerator(ComponentListGenerator.PublishJson);

            Assert.That(text, Is.Not.Empty);

            using var doc = JsonDocument.Parse(text);
            Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                Assert.That(prop.Value.ValueKind, Is.EqualTo(JsonValueKind.Array),
                    $"Component '{prop.Name}' must map to an array of entity ids.");
                Assert.That(prop.Value.GetArrayLength(), Is.GreaterThan(0),
                    $"Component '{prop.Name}' must be attached to at least one entity.");
            }
        });
    }

    [Test]
    public async Task PrototypeListJsonIsValid()
    {
        var server = Pair.Server;

        await server.WaitPost(() =>
        {
            var text = RunGenerator(PrototypeListGenerator.PublishJson);

            Assert.That(text, Is.Not.Empty);

            using var doc = JsonDocument.Parse(text);
            Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                Assert.That(prop.Value.ValueKind, Is.EqualTo(JsonValueKind.Array),
                    $"Prototype kind '{prop.Name}' must map to an array of ids.");
                Assert.That(prop.Value.GetArrayLength(), Is.GreaterThan(0),
                    $"Prototype kind '{prop.Name}' must have at least one prototype.");
            }
        });
    }

    [Test]
    public async Task TagJsonIsValid()
    {
        var server = Pair.Server;

        await server.WaitPost(() =>
        {
            var text = RunGenerator(TagJsonGenerator.PublishJson);

            Assert.That(text, Is.Not.Empty);

            using var doc = JsonDocument.Parse(text);
            Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                Assert.That(prop.Value.ValueKind, Is.EqualTo(JsonValueKind.Array),
                    $"Tag '{prop.Name}' must map to an array of entity ids.");
                Assert.That(prop.Value.GetArrayLength(), Is.GreaterThan(0),
                    $"Tag '{prop.Name}' must be attached to at least one entity.");
            }
        });
    }
}

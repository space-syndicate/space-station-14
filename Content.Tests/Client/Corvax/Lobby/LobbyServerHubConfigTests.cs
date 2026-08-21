using System.IO;
using System.Linq;
using Content.Client.Corvax.Lobby;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using YamlDotNet.RepresentationModel;

namespace Content.Tests.Client.Corvax.Lobby;

[TestFixture]
[TestOf(typeof(LobbyServerHubConfig))]
public sealed class LobbyServerHubConfigTests : ContentUnitTest
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        IoCManager.Resolve<IReflectionManager>().LoadAssemblies(typeof(LobbyServerHubConfig).Assembly);
        IoCManager.Resolve<ISerializationManager>().Initialize();
    }

    [Test]
    public void DeserializesTenServers()
    {
        const string yaml = """
            primary:
              - { address: "ss14://server1.example:1212", adult: true }
              - { address: "ss14://server2.example:1212" }
              - { address: "ss14://server3.example:1212" }
              - { address: "ss14://server4.example:1212" }
            subprojects:
              - { address: "ss14://server5.example:1212" }
              - { address: "ss14://server6.example:1212" }
              - { address: "ss14://server7.example:1212" }
              - { address: "ss14://server8.example:1212" }
              - { address: "ss14://server9.example:1212" }
              - { address: "ss14://server10.example:1212" }
            """;

        var config = Deserialize(yaml);
        var servers = config.AllServers.ToArray();

        Assert.That(servers, Has.Length.EqualTo(LobbyServerHubConfig.MaxServers));
        Assert.Multiple(() =>
        {
            Assert.That(config.Primary, Has.Count.EqualTo(4));
            Assert.That(config.Subprojects, Has.Count.EqualTo(6));
            Assert.That(servers[0].Adult, Is.True);
            Assert.That(servers[9].Address, Is.EqualTo("ss14://server10.example:1212"));
            Assert.That(servers, Has.All.Matches<LobbyServerEntry>(server => server.TryGetAddress(out _)));
        });
    }

    [TestCase("ss14://example.org:1212", true)]
    [TestCase("ss14s://example.org/server/main", true)]
    [TestCase("SS14://example.org:1212", true)]
    [TestCase("https://example.org:1212", false)]
    [TestCase("localhost:1212", false)]
    [TestCase("ss14://", false)]
    public void ValidatesLauncherAddresses(string value, bool expected)
    {
        var config = Deserialize($$"""
            primary:
              - address: "{{value}}"
            """);

        Assert.That(config.Primary[0].TryGetAddress(out _), Is.EqualTo(expected));
    }

    private static LobbyServerHubConfig Deserialize(string yaml)
    {
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();
        stream.Load(reader);

        var root = (YamlMappingNode) stream.Documents[0].RootNode;
        return IoCManager.Resolve<ISerializationManager>().Read<LobbyServerHubConfig>(new MappingDataNode(root));
    }
}

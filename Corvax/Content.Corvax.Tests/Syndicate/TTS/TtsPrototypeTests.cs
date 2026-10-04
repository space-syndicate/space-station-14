#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Corvax.TTS;
using Robust.Shared.Prototypes;

namespace Content.Corvax.Tests.Syndicate.TTS;

[TestFixture]
public sealed class TtsVoicePrototypeTest : GameTest
{
    public static readonly ProtoId<TTSVoicePrototype> DefaultVoice = "Taskmaster";
    private static readonly ProtoId<TTSVoicePrototype> AnnouncementVoice = "Glados";

    [Test]
    public async Task PrototypesAreValid()
    {
        var server = Pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var voices = protoManager.EnumeratePrototypes<TTSVoicePrototype>().ToList();
            Assert.That(voices, Is.Not.Empty, "There must be at least one TTS voice.");

            var seen = new HashSet<string>();
            foreach (var voice in voices)
            {
                Assert.That(voice.ID, Is.Not.Null.And.Not.Empty);
                Assert.That(seen.Add(voice.ID), Is.True, $"Duplicate voice ID: {voice.ID}");
                Assert.That(voice.Speaker, Is.Not.Null.And.Not.Empty, $"Voice {voice.ID} has empty Speaker.");
            }
        });
    }

    [Test]
    public async Task DefaultVoiceExists()
    {
        var server = Pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            Assert.That(protoManager.HasIndex(DefaultVoice), Is.True, "Default voice 'Taskmaster' must exist.");
        });
    }

    [Test]
    public async Task AnnouncementSpeakerExists()
    {
        var server = Pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            Assert.That(protoManager.HasIndex(AnnouncementVoice), Is.True, "Announcement speaker 'Glados' must exist.");
        });
    }
}

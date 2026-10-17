#nullable enable
using Content.IntegrationTests.Fixtures;

using ServerTTS = Content.Server.Corvax.TTS.TTSSystem;

namespace Content.Corvax.Tests.Syndicate.TTS;

[TestFixture]
public sealed class TtsSanitizeTest : GameTest
{
    [Test]
    public async Task ReplacesAbbreviations()
    {
        var server = Pair.Server;
        var tts = server.System<ServerTTS>();

        await server.WaitPost(() =>
        {
            Assert.That(TtsReflection.CallSanitize(tts, "нт"), Is.EqualTo("Эн Тэ"));
            Assert.That(TtsReflection.CallSanitize(tts, "гсб"), Is.EqualTo("Гэ Эс Бэ"));
            Assert.That(TtsReflection.CallSanitize(tts, "id"), Is.EqualTo("Ай Ди"));
        });
    }

    [Test]
    public async Task StripsInvalidCharacters()
    {
        var server = Pair.Server;
        var tts = server.System<ServerTTS>();

        await server.WaitPost(() =>
        {
            var result = TtsReflection.CallSanitize(tts, "Hello * World #");
            Assert.That(result, Does.Not.Contain("*"));
            Assert.That(result, Does.Not.Contain("#"));
        });
    }

    [Test]
    public async Task EmptyInputReturnsEmpty()
    {
        var server = Pair.Server;
        var tts = server.System<ServerTTS>();

        await server.WaitPost(() =>
        {
            Assert.That(TtsReflection.CallSanitize(tts, ""), Is.Empty);
            Assert.That(TtsReflection.CallSanitize(tts, "   "), Is.Empty);
        });
    }
}

[TestFixture]
public sealed class TtsSsmlTest : GameTest
{
    [Test]
    public async Task WrapsInSpeakTags()
    {
        var server = Pair.Server;
        var tts = server.System<ServerTTS>();

        await server.WaitPost(() =>
        {
            // Traits value here is irrelevant for the wrapping check; use 0.
            var ssml = TtsReflection.CallToSsml(tts, "Hello world", 0);
            Assert.That(ssml, Does.StartWith("<speak>"));
            Assert.That(ssml, Does.EndWith("</speak>"));
        });
    }

    [Test]
    public async Task EscapesXml()
    {
        var server = Pair.Server;
        var tts = server.System<ServerTTS>();

        await server.WaitPost(() =>
        {
            var ssml = TtsReflection.CallToSsml(tts, "<test> & \"q\"", 0);
            Assert.That(ssml, Does.Contain("&lt;test&gt;"));
            Assert.That(ssml, Does.Contain("&amp;"));
            Assert.That(ssml, Does.Contain("&quot;"));
        });
    }
}

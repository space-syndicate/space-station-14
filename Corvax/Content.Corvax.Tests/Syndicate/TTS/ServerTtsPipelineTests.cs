using Content.IntegrationTests.Fixtures;
using Content.Shared.Chat;
using Content.Shared.Corvax.TTS;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

using ServerTTS = Content.Server.Corvax.TTS.TTSSystem;
using ServerTTSManager = Content.Server.Corvax.TTS.TTSManager;

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

[TestFixture]
public sealed class TtsEntitySpokeTest : GameTest
{
    private static EntityUid SpawnSpeaker(
        IEntityManager entMan, SharedMapSystem mapSystem, MapId mapId, string? voiceId)
    {
        var mob = entMan.SpawnEntity("MobHuman", new MapCoordinates(0, 0, mapId));
        var tts = entMan.EnsureComponent<TTSComponent>(mob);
        tts.VoicePrototypeId = voiceId;
        return mob;
    }

    [Test]
    public async Task RunsWithCachedAudio()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var tts = server.System<ServerTTS>();
        var manager = server.ResolveDependency<ServerTTSManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);

            var voiceProto = protoManager.Index<TTSVoicePrototype>("Taskmaster");
            var mob = SpawnSpeaker(entMan, mapSystem, mapId, "Taskmaster");

            // Pre-compute the exact SSML GenerateTTS will produce and seed the cache.
            TtsReflection.ClearServerCache(manager);
            var ssml = TtsReflection.BuildSsmlFor(tts, "test", isWhisper: false);
            TtsReflection.PrepopulateServerCache(
                manager, voiceProto.Speaker, ssml, new byte[] { 1, 2, 3 });

            // Sanity: fail here, not on a silent network call if the SSML pipeline drifts.
            Assert.That(TtsReflection.HasServerCacheEntry(manager, voiceProto.Speaker, ssml),
                Is.True, "Sanity: cache must contain the seeded SSML entry.");

            // Raise EntitySpokeEvent on the mob; the system must hit the cache.
            var ev = new EntitySpokeEvent(mob, "test", "test", null, null);
            server.EntMan.EventBus.RaiseLocalEvent(mob, ev);

            entMan.DeleteEntity(mob);
            mapSystem.DeleteMap(mapId);
        });
    }

    [Test]
    public async Task IgnoresVeryLongMessage()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var mob = SpawnSpeaker(entMan, mapSystem, mapId, "Taskmaster");

            // 201 chars — over MaxMessageChars (200). The handler must return early
            // and never touch TTSManager (so no HTTP even without cache).
            var longMsg = new string('a', 201);
            var ev = new EntitySpokeEvent(mob, longMsg, longMsg, null, null);
            server.EntMan.EventBus.RaiseLocalEvent(mob, ev);

            entMan.DeleteEntity(mob);
            mapSystem.DeleteMap(mapId);
        });
    }

    [Test]
    public async Task IgnoresEntityWithoutVoice()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSystem = server.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            var mob = SpawnSpeaker(entMan, mapSystem, mapId, voiceId: null);

            var ev = new EntitySpokeEvent(mob, "test", "test", null, null);
            server.EntMan.EventBus.RaiseLocalEvent(mob, ev);

            entMan.DeleteEntity(mob);
            mapSystem.DeleteMap(mapId);
        });
    }
}

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Corvax.TTS;
using Robust.Shared.GameObjects;

using ClientTTS = Content.Client.Corvax.TTS.TTSSystem;

namespace Content.Corvax.Tests.Syndicate.TTS;

internal static class ClientTtsTestHelpers
{
    public static Dictionary<NetEntity, Queue<PlayTTSEvent>> GetQueues(ClientTTS tts)
        => (Dictionary<NetEntity, Queue<PlayTTSEvent>>)TtsReflection.ClientQueuesField.GetValue(tts)!;
}

[TestFixture]
public sealed class ClientTtsGatingTest : GameTest
{
    [Test]
    public async Task DisabledSkipsEvent()
    {
        var client = Pair.Client;
        var tts = client.System<ClientTTS>();

        await client.WaitPost(() =>
        {
            TtsReflection.ClearClientState(tts);
            TtsReflection.ClientEnabledField.SetValue(tts, false);

            var source = new NetEntity(1);
            client.EntMan.EventBus.RaiseEvent(EventSource.Local,
                new PlayTTSEvent(new byte[] { 1 }, source));

            Assert.That(ClientTtsTestHelpers.GetQueues(tts), Is.Empty,
                "Disabled TTS must not enqueue any playback.");
        });
    }

    [Test]
    public async Task RadioZeroVolumeSkipsEvent()
    {
        var client = Pair.Client;
        var tts = client.System<ClientTTS>();

        await client.WaitPost(() =>
        {
            TtsReflection.ClearClientState(tts);
            TtsReflection.ClientEnabledField.SetValue(tts, true);
            TtsReflection.ClientRadioVolumeField.SetValue(tts, 0f);

            var source = new NetEntity(1);
            client.EntMan.EventBus.RaiseEvent(EventSource.Local,
                new PlayTTSEvent(new byte[] { 1 }, source, isRadio: true));

            Assert.That(ClientTtsTestHelpers.GetQueues(tts), Is.Empty,
                "Radio TTS with zero volume must not enqueue.");
        });
    }

    [Test]
    public async Task ZeroVolumeSkipsEvent()
    {
        var client = Pair.Client;
        var tts = client.System<ClientTTS>();

        await client.WaitPost(() =>
        {
            TtsReflection.ClearClientState(tts);
            TtsReflection.ClientEnabledField.SetValue(tts, true);
            TtsReflection.ClientVolumeField.SetValue(tts, 0f);

            var source = new NetEntity(1);
            client.EntMan.EventBus.RaiseEvent(EventSource.Local,
                new PlayTTSEvent(new byte[] { 1 }, source));

            Assert.That(ClientTtsTestHelpers.GetQueues(tts), Is.Empty,
                "TTS with zero volume must not enqueue.");
        });
    }
}

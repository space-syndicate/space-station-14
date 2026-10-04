using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Corvax.TTS;
using Robust.Shared.GameObjects;

using ClientTTS = Content.Client.Corvax.TTS.TTSSystem;

namespace Content.Corvax.Tests.Syndicate.TTS;

internal static class ClientTtsTestHelpers
{
    public static Dictionary<NetEntity, Queue<PlayTTSEvent>> GetQueues(ClientTTS tts)
        => (Dictionary<NetEntity, Queue<PlayTTSEvent>>)TtsReflection.ClientQueuesField.GetValue(tts)!;

    public static void EnableWithFullVolume(ClientTTS tts)
    {
        TtsReflection.ClearClientState(tts);
        TtsReflection.ClientEnabledField.SetValue(tts, true);
        TtsReflection.ClientRadioVolumeField.SetValue(tts, 1.2f);
        TtsReflection.ClientVolumeField.SetValue(tts, 1.2f);
    }
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

[TestFixture]
public sealed class ClientTtsQueueTest : GameTest
{
    [Test]
    public async Task QueueOverflowDropsOldest()
    {
        var client = Pair.Client;
        var tts = client.System<ClientTTS>();

        await client.WaitPost(() =>
        {
            ClientTtsTestHelpers.EnableWithFullVolume(tts);

            var source = new NetEntity(1);

            // Force the entity into the "playing" state so incoming events enqueue
            // instead of trying to play immediately.
            var playing = (HashSet<NetEntity>)TtsReflection.ClientPlayingField.GetValue(tts)!;
            playing.Add(source);

            // Enqueue 7 events. The system keeps the queue at 6 max,
            // dropping the oldest when a new event arrives.
            for (var i = 0; i < 7; i++)
            {
                var payload = new byte[] { (byte)(i + 1) };
                client.EntMan.EventBus.RaiseEvent(EventSource.Local,
                    new PlayTTSEvent(payload, source));
            }

            var queues = ClientTtsTestHelpers.GetQueues(tts);
            Assert.That(queues.ContainsKey(source), Is.True, "Queue for source should exist.");

            var queue = queues[source];
            Assert.That(queue.Count, Is.EqualTo(6), "Queue must cap at 6 entries.");

            // The first event's payload (1) must have been dropped.
            Assert.That(queue.All(e => e.Data[0] != 1), Is.True,
                "Oldest event should have been dropped on overflow.");
        });
    }

    [Test]
    public async Task GlobalEventBypassesQueue()
    {
        var client = Pair.Client;
        var tts = client.System<ClientTTS>();

        await client.WaitPost(() =>
        {
            ClientTtsTestHelpers.EnableWithFullVolume(tts);

            // SourceUid == null → routed to PlayTTSInternal directly, bypassing the queue.
            // PlayTTSInternal will try to load the payload as audio; the fake byte is not
            // valid audio and may throw on some backends. The routing decision — "not queued" —
            // is what this test verifies, so audio errors are captured and reported, not
            // allowed to mask the actual assertion.
            Exception? playbackError = null;
            try
            {
                client.EntMan.EventBus.RaiseEvent(EventSource.Local,
                    new PlayTTSEvent(new byte[] { 1 }));
            }
            catch (Exception ex)
            {
                playbackError = ex;
            }

            // Primary assertion: nothing was queued for any source.
            Assert.That(ClientTtsTestHelpers.GetQueues(tts), Is.Empty,
                "Global (null source) must not be queued.");

            if (playbackError is not null)
            {
                // Not a failure — just useful diagnostics when audio plumbing changes.
                TestContext.WriteLine($"Note: PlayTTSInternal threw {playbackError.GetType().Name} " + "(expected for a fake audio payload).");
            }
        });
    }
}

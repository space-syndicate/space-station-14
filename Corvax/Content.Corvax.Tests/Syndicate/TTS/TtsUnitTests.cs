using System.Collections.Generic;
using Content.Server.Corvax.TTS;
using Content.Shared.Corvax.TTS;
using Robust.Shared.GameObjects;

namespace Content.Corvax.Tests.Syndicate.TTS;

[TestFixture]
public sealed class TtsComponentTest
{
    [Test]
    public void DefaultVoiceIsTaskmaster()
    {
        var comp = new TTSComponent();
        Assert.That(comp.VoicePrototypeId, Is.Not.Null);
        Assert.That(comp.VoicePrototypeId!.Value.Id, Is.EqualTo("Taskmaster"));
    }

    [Test]
    public void VoiceCanBeNulled()
    {
        var comp = new TTSComponent { VoicePrototypeId = null };
        Assert.That(comp.VoicePrototypeId, Is.Null);
    }
}

[TestFixture]
public sealed class TtsEventTest
{
    [Test]
    public void PlayEventStoresAllParameters()
    {
        var data = new byte[] { 1, 2, 3 };
        var source = new NetEntity(42);
        var ev = new PlayTTSEvent(data, source, isWhisper: true, isRadio: true);

        Assert.Multiple(() =>
        {
            Assert.That(ev.Data, Is.SameAs(data));
            Assert.That(ev.SourceUid, Is.EqualTo(source));
            Assert.That(ev.IsWhisper, Is.True);
            Assert.That(ev.IsRadio, Is.True);
        });
    }

    [Test]
    public void PlayEventDefaultsAreFalse()
    {
        var ev = new PlayTTSEvent(Array.Empty<byte>());

        Assert.Multiple(() =>
        {
            Assert.That(ev.SourceUid, Is.Null);
            Assert.That(ev.IsWhisper, Is.False);
            Assert.That(ev.IsRadio, Is.False);
        });
    }

    [Test]
    public void RequestPreviewEventStoresVoiceId()
    {
        var ev = new RequestPreviewTTSEvent("TestVoice");
        Assert.That(ev.VoiceId, Is.EqualTo("TestVoice"));
    }

    [Test]
    public void TransformSpeakerEventStoresVoiceId()
    {
        var sender = new EntityUid(1);
        var ev = new TransformSpeakerVoiceEvent(sender, "TestVoice");

        Assert.Multiple(() =>
        {
            Assert.That(ev.Sender, Is.EqualTo(sender));
            Assert.That(ev.VoiceId, Is.EqualTo("TestVoice"));
        });
    }
}

[TestFixture]
public sealed class RadioChannelFlagTest
{
    private static readonly RadioChannelFlag[] AllChannels =
    {
        RadioChannelFlag.Common,      RadioChannelFlag.Command,     RadioChannelFlag.Engineering,
        RadioChannelFlag.Medical,     RadioChannelFlag.Science,     RadioChannelFlag.Security,
        RadioChannelFlag.Service,     RadioChannelFlag.Supply,      RadioChannelFlag.Legal,
        RadioChannelFlag.Syndicate,   RadioChannelFlag.Binary,      RadioChannelFlag.Handheld,
        RadioChannelFlag.Freelance,   RadioChannelFlag.CentCom,     RadioChannelFlag.Xenoborg,
        RadioChannelFlag.Mothership,
    };

    [Test]
    public void EachChannelIsDistinctBit()
    {
        var seen = new HashSet<int>();
        foreach (var flag in AllChannels)
        {
            var value = (int)flag;
            Assert.That(value, Is.GreaterThan(0));
            Assert.That(value & (value - 1), Is.EqualTo(0), $"{flag} must be a single bit.");
            Assert.That(seen.Add(value), Is.True, $"Bit collision at {flag}.");
        }
    }

    [Test]
    public void AllExceptCommonExcludesCommon()
    {
        Assert.That(RadioChannelFlag.AllExceptCommon.HasFlag(RadioChannelFlag.Common), Is.False);
    }

    [Test]
    public void AllIncludesEveryChannel()
    {
        foreach (var flag in AllChannels)
        {
            Assert.That(RadioChannelFlag.All.HasFlag(flag), Is.True, $"All must include {flag}.");
        }
    }
}

[TestFixture]
public sealed class TtsVoiceEffectPresetTest
{
    [Test]
    public void EnumValuesAreStable()
    {
        // These values are networked; changing them breaks clients.
        Assert.That((int)TTSVoiceEffectPreset.None,     Is.EqualTo(0));
        Assert.That((int)TTSVoiceEffectPreset.Room,     Is.EqualTo(1));
        Assert.That((int)TTSVoiceEffectPreset.Hall,     Is.EqualTo(2));
        Assert.That((int)TTSVoiceEffectPreset.Void,     Is.EqualTo(3));
        Assert.That((int)TTSVoiceEffectPreset.Airlock,  Is.EqualTo(4));
        Assert.That((int)TTSVoiceEffectPreset.Warm,     Is.EqualTo(5));
        Assert.That((int)TTSVoiceEffectPreset.Subspace, Is.EqualTo(6));
    }
}

[TestFixture]
public sealed class NumberConverterTest
{
    [TestCase(0L, "ноль")]
    [TestCase(1L, "один")]
    [TestCase(10L, "десять")]
    [TestCase(21L, "двадцать один")]
    [TestCase(1000L, "одна тысяча")]
    [TestCase(2000L, "две тысячи")]
    [TestCase(5000L, "пять тысяч")]
    [TestCase(1_000_000L, "один миллион")]
    public void ConvertsNumber(long input, string expected)
    {
        Assert.That(NumberConverter.NumberToText(input), Is.EqualTo(expected));
    }

    [Test]
    public void NegativePrefixedWithMinus()
    {
        Assert.That(NumberConverter.NumberToText(-5), Does.StartWith("минус"));
    }

    [Test]
    public void OutOfRangeReturnsEmpty()
    {
        Assert.That(NumberConverter.NumberToText(1_000_000_000_000_000), Is.Empty);
    }
}

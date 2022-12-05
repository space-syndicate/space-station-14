using Content.Server.Chat.Systems;
using Content.Shared.CCVar;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;

    private const int MAX_CHARACTERS = 500;
    private bool _isEnabled = false;
    
    public override void Initialize()
    {
        _cfg.OnValueChanged(CCVars.TTSEnabled, v => _isEnabled = v, true);
        
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        if (!_isEnabled ||
            args.Message.Length > MAX_CHARACTERS ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(component.VoicePrototypeId, out var protoVoice))
            return;

        var textSsml = ToSsmlText(args.Message, SpeechRate.Fast);
        var soundData = await _ttsManager.ConvertTextToSpeech(protoVoice.Speaker, textSsml);
        RaiseNetworkEvent(new PlayTTSEvent(uid, soundData), Filter.Pvs(uid));
    }
    
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private string ToSsmlText(string text, SpeechRate rate = SpeechRate.Medium)
    {
        return $"<speak><prosody rate=\"{SpeechRateMap[rate]}\">{text}</prosody></speak>";
    }

    private enum SpeechRate : byte
    {
        VerySlow,
        Slow,
        Medium,
        Fast,
        VeryFast,
    }

    private static readonly IReadOnlyDictionary<SpeechRate, string> SpeechRateMap =
        new Dictionary<SpeechRate, string>()
        {
            {SpeechRate.VerySlow, "x-slow"},
            {SpeechRate.Slow, "slow"},
            {SpeechRate.Medium, "medium"},
            {SpeechRate.Fast, "fast"},
            {SpeechRate.VeryFast, "x-fast"},
        };
}

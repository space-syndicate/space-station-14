using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;

    private const int MaxMessageChars = 100; // same as SingleBubbleCharLimit
    private bool _isEnabled = false;
    
    public override void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.TTSEnabled, v => _isEnabled = v, true);
        
        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        _netMgr.RegisterNetMessage<MsgRequestTTS>(OnRequestTTS);
    }

    private async void OnRequestTTS(MsgRequestTTS ev)
    {
        if (!_playerManager.TryGetSessionByChannel(ev.MsgChannel, out var session) ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        var soundData = await GenerateTTS(ev.Text, protoVoice.Speaker);
        RaiseNetworkEvent(new PlayTTSEvent(ev.Uid, soundData), Filter.SinglePlayer(session));
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        if (!_isEnabled ||
            args.OriginalMessage.Length > MaxMessageChars ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(component.VoicePrototypeId, out var protoVoice))
            return;

        var soundData = await GenerateTTS(args.OriginalMessage, protoVoice.Speaker);
        RaiseNetworkEvent(new PlayTTSEvent(uid, soundData), Filter.Pvs(uid));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }
    
    // ReSharper disable once InconsistentNaming
    private async Task<byte[]> GenerateTTS(string text, string speaker)
    {
        var textSanitized = Sanitize(text);
        var textSsml = ToSsmlText(textSanitized, SpeechRate.Fast);
        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}

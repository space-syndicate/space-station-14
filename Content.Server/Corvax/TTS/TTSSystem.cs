using Content.Server.Chat.Systems;
using Content.Shared.CCVar;
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
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    private bool _isEnabled = false;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        _cfg.OnValueChanged(CCVars.TTSEnabled, v => _isEnabled = v, true);
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        if (!_isEnabled || !_prototypeManager.TryIndex<TTSVoicePrototype>(component.VoicePrototypeId, out var protoVoice))
        {
            return;
        }
        
        var sound = await _ttsManager.ConvertTextToSpeech(protoVoice.Speaker, args.Message);
        _audioSystem.Play(
            $"/Uploaded/{sound.Path}",
            Filter.Broadcast(),
            uid,
            AudioParams.Default.WithAttenuation(Attenuation.LinearDistance));
    }
}
using Content.Shared.Corvax.TTS;
using Robust.Shared.Audio.Components;

namespace Content.Client.Corvax.TTS;

public sealed partial class TTSSystem
{
    private EntityUid? _cachedVoiceEffectEntity;
    private EntityUid? _cachedRadioEffectEntity;
    private readonly object _voiceEffectLock = new();
    private readonly object _radioEffectLock = new();

    /// <summary>
    /// Clears all EFX-effects.
    /// </summary>
    private void ShutdownEffects()
    {
        if (_cachedVoiceEffectEntity != null && !TerminatingOrDeleted(_cachedVoiceEffectEntity.Value))
        {
            _audio.Stop(_cachedVoiceEffectEntity);
            Del(_cachedVoiceEffectEntity);
        }

        _cachedVoiceEffectEntity = null;

        if (_cachedRadioEffectEntity != null && !TerminatingOrDeleted(_cachedRadioEffectEntity.Value))
        {
            _audio.Stop(_cachedRadioEffectEntity);
            Del(_cachedRadioEffectEntity);
        }

        _cachedRadioEffectEntity = null;
    }

    private void ApplyVoiceEffect((EntityUid Entity, AudioComponent Component) audio, TTSVoiceEffectPreset effect)
    {
        if (effect == TTSVoiceEffectPreset.None)
            return;

        if (_cachedVoiceEffectEntity == null)
        {
            if (!EnsureVoiceEffectInitialized())
            {
                if (_cachedVoiceEffectEntity == null)
                    return;
            }
        }

        if (_cachedVoiceEffectEntity == null)
            return;

        try
        {
            var (entity, comp) = audio;
            var (auxUid, auxComp) = _audio.CreateAuxiliary();

            _audio.SetEffect(auxUid, auxComp, _cachedVoiceEffectEntity.Value);
            _audio.SetAuxiliary(entity, comp, auxUid);

            _sawmill.Verbose($"Applied voice effect ({effect}) to audio entity {entity}");
        }
        catch (Exception ex)
        {
            _sawmill.Debug($"Failed to apply voice effect: {ex.Message}");
        }
    }

    private void ApplyRadioEffect((EntityUid Entity, AudioComponent Component) audio)
    {
        if (!EnsureRadioEffectInitialized())
        {
            if (_cachedRadioEffectEntity == null)
                return;
        }

        if (_cachedRadioEffectEntity == null)
            return;

        try
        {
            var (entity, comp) = audio;
            var (auxUid, auxComp) = _audio.CreateAuxiliary();

            _audio.SetEffect(auxUid, auxComp, _cachedRadioEffectEntity.Value);
            _audio.SetAuxiliary(entity, comp, auxUid);

            _sawmill.Debug($"Applied radio EFX effect to audio entity {entity}");
        }
        catch (Exception ex)
        {
            _sawmill.Debug($"Failed to apply radio EFX effect: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes voice effect upon first use, if necessary.
    /// </summary>
    private bool EnsureVoiceEffectInitialized()
    {
        if (_voiceEffectPreset == TTSVoiceEffectPreset.None)
        {
            _cachedVoiceEffectEntity = null;
            return false;
        }

        if (_cachedVoiceEffectEntity != null)
            return true;

        lock (_voiceEffectLock)
        {
            if (_cachedVoiceEffectEntity != null)
                return true;

            if (_voiceEffectPreset == TTSVoiceEffectPreset.None)
            {
                _cachedVoiceEffectEntity = null;
                return false;
            }

            try
            {
                _sawmill.Debug($"Initializing voice effect for preset: {_voiceEffectPreset}");

                var effectResult = _audio.CreateEffect();
                var (effectUid, effectComp) = effectResult;
                _cachedVoiceEffectEntity = effectUid;

                var preset = GetVoicePreset(_voiceEffectPreset);
                _audio.SetEffectPreset(effectUid, effectComp, preset);

                _sawmill.Info($"Voice effect initialized: {_voiceEffectPreset}");
                return true;
            }
            catch (Exception ex)
            {
                _sawmill.Warning($"Failed to initialize voice effect: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Initializes radio effect on first use.
    /// </summary>
    private bool EnsureRadioEffectInitialized()
    {
        if (_cachedRadioEffectEntity != null)
            return true;

        lock (_radioEffectLock)
        {
            if (_cachedRadioEffectEntity != null)
                return true;

            try
            {
                _sawmill.Debug("Initializing radio EFX effect...");

                var effectResult = _audio.CreateEffect();
                var (effectUid, effectComp) = effectResult;
                _cachedRadioEffectEntity = effectUid;

                var radioPreset = CreateRadioPreset();
                _audio.SetEffectPreset(effectUid, effectComp, radioPreset);

                return true;
            }
            catch (Exception ex)
            {
                _sawmill.Warning($"Failed to initialize radio EFX effect: {ex.Message}");
                return false;
            }
        }
    }
}

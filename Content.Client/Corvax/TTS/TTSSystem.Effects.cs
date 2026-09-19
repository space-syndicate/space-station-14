using Content.Shared.Corvax.TTS;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Effects;

namespace Content.Client.Corvax.TTS;

public sealed partial class TTSSystem
{
    private EntityUid? _radioAuxiliaryEntity;
    private EntityUid? _voiceAuxiliaryEntity;
    private EntityUid? _cachedVoiceEffectEntity;
    private EntityUid? _cachedRadioEffectEntity;
    private readonly object _voiceEffectLock = new();
    private readonly object _radioEffectLock = new();

    /// <summary>
    /// It frees up only the vocal effect and its auxiliary.
    /// </summary>
    private void ShutdownVoiceEffect()
    {
        if (_cachedVoiceEffectEntity != null && !TerminatingOrDeleted(_cachedVoiceEffectEntity.Value))
        {
            _audio.Stop(_cachedVoiceEffectEntity);
            Del(_cachedVoiceEffectEntity);
        }
        _cachedVoiceEffectEntity = null;

        if (_voiceAuxiliaryEntity != null && !TerminatingOrDeleted(_voiceAuxiliaryEntity.Value))
        {
            if (TryComp<AudioAuxiliaryComponent>(_voiceAuxiliaryEntity.Value, out var auxComp))
            {
                auxComp.Auxiliary?.SetEffect(null);
                auxComp.Auxiliary?.Dispose();
            }
            Del(_voiceAuxiliaryEntity);
        }
        _voiceAuxiliaryEntity = null;
    }

    /// <summary>
    /// It frees up only the radio effect and its auxiliary.
    /// </summary>
    private void ShutdownRadioEffect()
    {
        if (_cachedRadioEffectEntity != null && !TerminatingOrDeleted(_cachedRadioEffectEntity.Value))
        {
            _audio.Stop(_cachedRadioEffectEntity);
            Del(_cachedRadioEffectEntity);
        }
        _cachedRadioEffectEntity = null;

        if (_radioAuxiliaryEntity != null && !TerminatingOrDeleted(_radioAuxiliaryEntity.Value))
        {
            if (TryComp<AudioAuxiliaryComponent>(_radioAuxiliaryEntity.Value, out var auxComp))
            {
                auxComp.Auxiliary?.SetEffect(null);
                auxComp.Auxiliary?.Dispose();
            }
            Del(_radioAuxiliaryEntity);
        }
        _radioAuxiliaryEntity = null;
    }

    /// <summary>
    /// Clears all EFX-effects.
    /// </summary>
    private void ShutdownEffects()
    {
        ShutdownVoiceEffect();
        ShutdownRadioEffect();
    }

    private void ApplyVoiceEffect((EntityUid Entity, AudioComponent Component) audio, TTSVoiceEffectPreset effect)
    {
        if (!_ttsEnabled)
            return;

        if (effect == TTSVoiceEffectPreset.None)
            return;

        if (!EnsureVoiceEffectInitialized())
            return;

        if (_voiceAuxiliaryEntity == null)
            return;

        try
        {
            var (entity, comp) = audio;
            _audio.SetAuxiliary(entity, comp, _voiceAuxiliaryEntity.Value);
        }
        catch (Exception ex)
        {
            _sawmill.Debug($"Failed to apply voice effect: {ex.Message}");
        }
    }

    private void ApplyRadioEffect((EntityUid Entity, AudioComponent Component) audio)
    {
        if (!_ttsEnabled)
            return;

        if (!EnsureRadioEffectInitialized())
            return;

        if (_radioAuxiliaryEntity == null)
            return;

        try
        {
            var (entity, comp) = audio;
            _audio.SetAuxiliary(entity, comp, _radioAuxiliaryEntity.Value);
        }
        catch (Exception ex)
        {
            _sawmill.Debug($"Failed to apply radio EFX effect: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an effect and an auxiliary, configures the preset, and links them.
    /// Rolls back everything created and returns null if any error occurs.
    /// </summary>
    private (EntityUid Effect, EntityUid Auxiliary)? TryCreateEffectWithAuxiliary(ReverbProperties preset, string debugName)
    {
        EntityUid? effectUid = null;
        EntityUid? auxUid = null;

        try
        {
            var (effect, effectComp) = _audio.CreateEffect();
            effectUid = effect;

            _audio.SetEffectPreset(effect, effectComp, preset);

            var (aux, auxComp) = _audio.CreateAuxiliary();
            auxUid = aux;

            _audio.SetEffect(aux, auxComp, effect);

            return (effect, aux);
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Failed to initialize {debugName} effect: {ex.Message}");
            if (auxUid != null && !TerminatingOrDeleted(auxUid.Value))
            {
                if (TryComp<AudioAuxiliaryComponent>(auxUid.Value, out var auxComp))
                {
                    auxComp.Auxiliary?.SetEffect(null);
                    auxComp.Auxiliary?.Dispose();
                }
                Del(auxUid.Value);
            }

            if (effectUid != null && !TerminatingOrDeleted(effectUid.Value))
            {
                _audio.Stop(effectUid.Value);
                Del(effectUid.Value);
            }

            return null;
        }
    }

    /// <summary>
    /// Initializes voice effect upon first use, if necessary.
    /// </summary>
    private bool EnsureVoiceEffectInitialized()
    {
        if (!_ttsEnabled)
            return false;

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

            var result = TryCreateEffectWithAuxiliary(GetVoicePreset(_voiceEffectPreset), $"voice ({_voiceEffectPreset})");
            if (result == null)
                return false;

            _cachedVoiceEffectEntity = result.Value.Effect;
            _voiceAuxiliaryEntity = result.Value.Auxiliary;

            _sawmill.Info($"Voice effect initialized: {_voiceEffectPreset}");
            return true;
        }
    }

    /// <summary>
    /// Initializes radio effect on first use.
    /// </summary>
    private bool EnsureRadioEffectInitialized()
    {
        if (!_ttsEnabled)
            return false;

        if (_cachedRadioEffectEntity != null)
            return true;

        lock (_radioEffectLock)
        {
            if (_cachedRadioEffectEntity != null)
                return true;

            var result = TryCreateEffectWithAuxiliary(CreateRadioPreset(), "radio");
            if (result == null)
                return false;

            _cachedRadioEffectEntity = result.Value.Effect;
            _radioAuxiliaryEntity = result.Value.Auxiliary;

            return true;
        }
    }
}

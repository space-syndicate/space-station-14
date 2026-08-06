using Content.Shared.Corvax.TTS;
using Robust.Shared.Audio.Components;

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

        if (_cachedVoiceEffectEntity == null || _voiceAuxiliaryEntity == null)
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
        if (!EnsureRadioEffectInitialized())
        {
            if (_cachedRadioEffectEntity == null)
                return;
        }

        if (_cachedRadioEffectEntity == null || _radioAuxiliaryEntity == null)
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

                var (auxUid, auxComp) = _audio.CreateAuxiliary();
                _voiceAuxiliaryEntity = auxUid;

                _audio.SetEffect(auxUid, auxComp, effectUid);

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
                var effectResult = _audio.CreateEffect();
                var (effectUid, effectComp) = effectResult;
                _cachedRadioEffectEntity = effectUid;

                var radioPreset = CreateRadioPreset();
                _audio.SetEffectPreset(effectUid, effectComp, radioPreset);

                var (auxUid, auxComp) = _audio.CreateAuxiliary();
                _radioAuxiliaryEntity = auxUid;

                _audio.SetEffect(auxUid, auxComp, effectUid);

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

using System.Numerics;
using Content.Shared.Corvax.TTS;
using Robust.Shared.Audio.Effects;

namespace Content.Client.Corvax.TTS;

public sealed partial class TTSSystem
{
    /// <summary>
    /// Returns ReverbProperties for the selected preset.
    /// </summary>
    private ReverbProperties GetVoicePreset(TTSVoiceEffectPreset preset)
    {
        return preset switch
        {
            TTSVoiceEffectPreset.None => ReverbPresets.Generic,
            TTSVoiceEffectPreset.Room => ReverbPresets.Room,
            TTSVoiceEffectPreset.Hall => ReverbPresets.SpaceStationHall,

            TTSVoiceEffectPreset.Void => CreateVoidPreset(),
            TTSVoiceEffectPreset.Airlock => CreateAirlockPreset(),
            TTSVoiceEffectPreset.Warm => CreateWarmPreset(),
            TTSVoiceEffectPreset.Subspace => CreateSubspacePreset(),

            _ => ReverbPresets.Generic
        };
    }

    private ReverbProperties CreateVoidPreset()
    {
        return new ReverbProperties(
            density: 0.3f,
            diffusion: 0.4f,
            gain: 0.15f,
            gainHF: 0.05f,
            gainLF: 0.1f,
            decayTime: 1.2f,
            decayHFRatio: 0.3f,
            decayLFRatio: 0.5f,
            reflectionsGain: 0.05f,
            reflectionsDelay: 0.02f,
            reflectionsPan: Vector3.Zero,
            lateReverbGain: 0.2f,
            lateReverbDelay: 0.04f,
            lateReverbPan: Vector3.Zero,
            echoTime: 0.1f,
            echoDepth: 0.2f,
            modulationTime: 0.05f,
            modulationDepth: 0.0f,
            airAbsorptionGainHF: 0.95f,
            hfReference: 6000f,
            lfReference: 150f,
            roomRolloffFactor: 0.0f,
            decayHFLimit: 0
        );
    }

    private ReverbProperties CreateAirlockPreset()
    {
        return new ReverbProperties(
            density: 0.8f,
            diffusion: 0.5f,
            gain: 0.3f,
            gainHF: 0.35f,
            gainLF: 0.1f,
            decayTime: 2.0f,
            decayHFRatio: 0.8f,
            decayLFRatio: 0.2f,
            reflectionsGain: 0.5f,
            reflectionsDelay: 0.015f,
            reflectionsPan: new Vector3(0.2f, 0.0f, 0.0f),
            lateReverbGain: 0.4f,
            lateReverbDelay: 0.025f,
            lateReverbPan: new Vector3(-0.2f, 0.0f, 0.0f),
            echoTime: 0.08f,
            echoDepth: 0.5f,
            modulationTime: 0.05f,
            modulationDepth: 0.0f,
            airAbsorptionGainHF: 0.95f,
            hfReference: 5000f,
            lfReference: 200f,
            roomRolloffFactor: 0.0f,
            decayHFLimit: 0
        );
    }

    private ReverbProperties CreateWarmPreset()
    {
        return new ReverbProperties(
            density: 0.9f,
            diffusion: 0.9f,
            gain: 0.25f,
            gainHF: 0.1f,
            gainLF: 0.5f,
            decayTime: 1.0f,
            decayHFRatio: 0.3f,
            decayLFRatio: 0.8f,
            reflectionsGain: 0.3f,
            reflectionsDelay: 0.01f,
            reflectionsPan: Vector3.Zero,
            lateReverbGain: 0.2f,
            lateReverbDelay: 0.015f,
            lateReverbPan: Vector3.Zero,
            echoTime: 0.075f,
            echoDepth: 0.05f,
            modulationTime: 0.05f,
            modulationDepth: 0.0f,
            airAbsorptionGainHF: 0.95f,
            hfReference: 4000f,
            lfReference: 100f,
            roomRolloffFactor: 0.0f,
            decayHFLimit: 0
        );
    }

    private ReverbProperties CreateSubspacePreset()
    {
        return new ReverbProperties(
            density: 0.4f,
            diffusion: 0.3f,
            gain: 0.3f,
            gainHF: 0.4f,
            gainLF: 0.1f,
            decayTime: 3.2f,
            decayHFRatio: 0.8f,
            decayLFRatio: 0.2f,
            reflectionsGain: 0.1f,
            reflectionsDelay: 0.05f,
            reflectionsPan: new Vector3(0.5f, 0.0f, 0.0f),
            lateReverbGain: 0.6f,
            lateReverbDelay: 0.08f,
            lateReverbPan: new Vector3(-0.5f, 0.0f, 0.0f),
            echoTime: 0.2f,
            echoDepth: 0.6f,
            modulationTime: 0.05f,
            modulationDepth: 0.2f,
            airAbsorptionGainHF: 0.95f,
            hfReference: 6000f,
            lfReference: 100f,
            roomRolloffFactor: 0.0f,
            decayHFLimit: 0
        );
    }

    private ReverbProperties CreateRadioPreset()
    {
        return new ReverbProperties(
            density: 1.0f,
            diffusion: 0.1f,
            gain: 0.3f,
            gainHF: 0.01f,
            gainLF: 0.05f,
            decayTime: 0.15f,
            decayHFRatio: 0.9f,
            decayLFRatio: 0.1f,
            reflectionsGain: 0.15f,
            reflectionsDelay: 0.005f,
            reflectionsPan: new Vector3(0.1f, 0.0f, 0.0f),
            lateReverbGain: 0.8f,
            lateReverbDelay: 0.02f,
            lateReverbPan: new Vector3(-0.1f, 0.0f, 0.0f),
            echoTime: 0.1f,
            echoDepth: 0.8f,
            modulationTime: 0.25f,
            modulationDepth: 0.7f,
            airAbsorptionGainHF: 0.994f,
            hfReference: 2000f,
            lfReference: 100f,
            roomRolloffFactor: 0.0f,
            decayHFLimit: 0
        );
    }
}

namespace Content.Shared.Corvax.TTS;

/// <summary>
/// Available voice effect presets for TTS.
/// </summary>
public enum TTSVoiceEffectPreset : int
{
    None = 0, // No effect - classic voice.
    Room = 1,
    Hall = 2,
    Void = 3,
    Airlock = 4,
    Warm = 5,
    Subspace = 6,
}

using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.TTS;

/// <summary>
/// Prototype represent available TTS voices
/// </summary>
[Prototype("ttsVoice")]
// ReSharper disable once InconsistentNaming
public sealed class TTSVoicePrototype : IPrototype
{
    private string _name = string.Empty;

    [IdDataField]
    public string ID { get; } = default!;
    
    [DataField("name")]
    public string Name
    {
        get => _name;
        private set => _name = Loc.GetString(value);
    }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("speaker")]
    public string Speaker { get; } = string.Empty;
}
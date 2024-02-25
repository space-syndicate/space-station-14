using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Content.Shared.Humanoid;

namespace Content.Server.VoiceMask;

[RegisterComponent]
public sealed partial class VoiceMaskComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string VoiceName = "Unknown";

    // Corvax-TTS-Start
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string VoiceId = SharedHumanoidAppearanceSystem.DefaultVoice;
    // Corvax-TTS-End

    /// <summary>
    /// If EnableSpeechVerbModification is true, overrides the speech verb used when this entity speaks.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SpeechVerbPrototype>? SpeechVerb;
}

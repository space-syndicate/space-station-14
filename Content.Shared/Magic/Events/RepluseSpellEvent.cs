using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Magic.Events;

public sealed partial class RepulseSpellEvent : InstantActionEvent, ISpeakSpell
{

    [DataField("range")]
    public float Range;

    [DataField("strength")]
    public float Strength;

    [DataField("speech")]
    public string? Speech { get; private set; }
}

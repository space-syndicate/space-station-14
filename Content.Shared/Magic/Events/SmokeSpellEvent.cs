using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Magic.Events;

public sealed partial class SmokeSpellEvent : InstantActionEvent, ISpeakSpell
{

    [DataField("duration")]
    public float Duration;

    [DataField("spreadAmount")]
    public int SpreadAmount;

    [DataField("speech")]
    public string? Speech { get; private set; }

    /// <summary>
    /// Gets the targeted spawn positons; may lead to multiple entities being spawned.
    /// </summary>
    [DataField("posData")] public MagicSpawnData Pos = new TargetCasterPos();
}

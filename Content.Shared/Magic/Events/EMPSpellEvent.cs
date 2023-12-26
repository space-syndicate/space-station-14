using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Magic.Events;

public sealed partial class EMPSpellEvent : InstantActionEvent, ISpeakSpell
{

    [DataField("range")]
    public float Range;

    [DataField("disableDuration")]
    public float DisableDuration;

    [DataField("energyConsumption")]
    public float EnergyConsumption;

    [DataField("speech")]
    public string? Speech { get; private set; }

    /// <summary>
    /// Gets the targeted spawn positons; may lead to multiple entities being spawned.
    /// </summary>
    [DataField("posData")] public MagicSpawnData Pos = new TargetCasterPos();
}

using Robust.Shared.Prototypes;
using Content.Shared.Random;
using Content.Shared.Silicons.Laws;
using Robust.Shared.Audio;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent]
public sealed partial class StationAIComponent : Component
{
    [DataField("action")]
    public EntProtoId Action = "AIHealthOverlay";

    public EntityUid? ActionId;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ActiveEye = EntityUid.Invalid;

    [DataField("lawsId")]
    public ProtoId<WeightedRandomPrototype> LawsId = "LawsStationAIDefault";

    [ViewVariables(VVAccess.ReadWrite)]
    public SiliconLawsetPrototype? SelectedLaw;

    [DataField("aiDronePrototype")]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId AiDronePrototype = "SAIDrone";

    [DataField("aiDroneChangeActionPrototype")]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId AiDroneChangeActionPrototype = "ActionAIDroneChange";

    public EntityUid? AiDroneChangeAction = null;

    [DataField("aiDrone")]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? AiDrone = null;

    public readonly SoundSpecifier AiDeath =
        new SoundPathSpecifier("/Audio/Machines/AI/borg_death.ogg");
}

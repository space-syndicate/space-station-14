using Robust.Shared.Prototypes;
using Content.Shared.Random;
using Content.Shared.Silicons.Laws;

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
}

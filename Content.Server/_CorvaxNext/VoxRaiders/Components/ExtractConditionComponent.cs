using Content.Shared.Objectives;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.VoxRaiders.Components;

[RegisterComponent]
public sealed partial class ExtractConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<StealTargetGroupPrototype> StealGroup;
}

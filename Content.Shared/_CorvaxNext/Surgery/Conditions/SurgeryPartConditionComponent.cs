using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartConditionComponent : Component
{
    [DataField]
    public BodyPartType Part;

    [DataField]
    public BodyPartSymmetry? Symmetry;

    [DataField]
    public bool Inverse;
}
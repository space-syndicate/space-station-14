using Content.Shared.Mind;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.VoxRaiders.Components;

[RegisterComponent]
public sealed partial class VoxRaidersRuleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<WeightedRandomPrototype> ObjectiveGroup;

    [DataField(required: true)]
    public int ObjectiveCount;

    [DataField]
    public List<string> ObjectivePrototypes = [];

    public Dictionary<string, List<(EntityUid Objective, Entity<MindComponent> Mind)>> Objectives = [];

    public EntityUid Map;

    public bool Success;
}

using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(HereticRuleSystem))]
public sealed partial class HereticRuleComponent : Component
{
    public readonly List<EntityUid> Minds = [];

    public readonly List<ProtoId<StoreCategoryPrototype>> StoreCategories =
    [
        "HereticPathAsh",
        //"HereticPathLock",
        "HereticPathFlesh",
        "HereticPathBlade",
        "HereticPathVoid",
        "HereticPathSide"
    ];

    public readonly List<EntProtoId> Objectives =
    [
        "HereticKnowledgeObjective",
        "HereticSacrificeObjective",
        "HereticSacrificeHeadObjective"
    ];
}

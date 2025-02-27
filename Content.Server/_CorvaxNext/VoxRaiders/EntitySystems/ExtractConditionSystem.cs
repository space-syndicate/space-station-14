using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Server.Objectives.Components.Targets;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.VoxRaiders.EntitySystems;

public sealed class ExtractConditionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ExtractConditionComponent, ObjectiveAfterAssignEvent>(OnObjectiveAfterAssign);
        SubscribeLocalEvent<ExtractConditionComponent, ObjectiveGetProgressEvent>(OnObjectiveGetProgress);
    }

    private void OnObjectiveAfterAssign(Entity<ExtractConditionComponent> entity, ref ObjectiveAfterAssignEvent e)
    {
        var group = _prototype.Index(entity.Comp.StealGroup);

        var localizedName = Loc.GetString(group.Name);

        var title = Loc.GetString("objective-condition-steal-title-no-owner", ("itemName", localizedName));

        var description = Loc.GetString("objective-condition-steal-description", ("itemName", localizedName));

        _meta.SetEntityName(entity, title, e.Meta);
        _meta.SetEntityDescription(entity, description, e.Meta);
        _objectives.SetIcon(entity, group.Sprite, e.Objective);
    }

    private void OnObjectiveGetProgress(Entity<ExtractConditionComponent> entity, ref ObjectiveGetProgressEvent e)
    {
        var query = AllEntityQuery<StealTargetComponent, TransformComponent>();
        while (query.MoveNext(out var target, out var transform))
        {
            if (target.StealGroup != entity.Comp.StealGroup)
                continue;

            if (!TryComp<ExtractionMapComponent>(transform.MapUid, out var map))
                continue;

            if (!map.Owners.Contains(e.MindId))
                continue;

            e.Progress = 1;

            return;
        }

        e.Progress = 0;
    }
}

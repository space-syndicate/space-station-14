using System.Linq;
using System.Text;
using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._CorvaxNext.VoxRaiders.EntitySystems;

public sealed class VoxRaidersRuleSystem : GameRuleSystem<VoxRaidersRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoxRaidersRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
        SubscribeLocalEvent<VoxRaidersRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
    }

    protected override void Started(EntityUid uid, VoxRaidersRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (!_prototype.TryIndex(component.ObjectiveGroup, out var objectiveGroup))
            return;

        var groups = objectiveGroup.Weights.ShallowClone();

        for (var i = 0; i < component.ObjectiveCount; i++)
        {
            if (!_random.TryPickAndTake(groups, out var objective))
                break;

            component.ObjectivePrototypes.Add(objective);
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, VoxRaidersRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        StringBuilder objectivesBuilder = new();

        var globalSuccess = true;

        foreach (var objectives in component.Objectives.Values)
        {
            if (objectives.Count < 1)
                continue;

            var success = objectives.Any(objective => _objectives.IsCompleted(objective.Objective, objective.Mind));

            var info = _objectives.GetInfo(objectives[0].Objective, objectives[0].Mind, objectives[0].Mind);

            if (info is null)
                continue;

            if (!success)
            {
                globalSuccess = false;
                objectivesBuilder.AppendLine(Loc.GetString("objectives-objective-fail", ("objective", info.Value.Title), ("progress", 0), ("markupColor", "red")));
            }
            else
                objectivesBuilder.AppendLine(Loc.GetString("objectives-objective-success", ("objective", info.Value.Title), ("markupColor", "green")));
        }

        StringBuilder builder = new();

        builder.AppendLine(Loc.GetString(globalSuccess ? "vox-raiders-success" : "vox-raiders-fail"));
        builder.AppendLine(Loc.GetString("vox-raiders-objectives"));
        builder.Append(objectivesBuilder);

        args.AddLine(builder.ToString());
    }

    private void OnAfterAntagEntitySelected(Entity<VoxRaidersRuleComponent> entity, ref AfterAntagEntitySelectedEvent e)
    {
        var mind = e.Session?.GetMind();

        if (mind is null)
            return;

        entity.Comp.Shuttle?.Owners.Add(mind.Value);

        foreach (var objective in entity.Comp.ObjectivePrototypes)
        {
            if (!TryComp<MindComponent>(mind.Value, out var mindComponent))
                continue;

            var obj = _objectives.TryCreateObjective(mind.Value, mindComponent, objective);

            if (obj is null)
                continue;

            _mind.AddObjective(mind.Value, mindComponent, obj.Value);

            entity.Comp.Objectives.GetOrNew(objective).Add((obj.Value, (mind.Value, mindComponent)));
        }
    }

    private void OnRuleLoadedGrids(Entity<VoxRaidersRuleComponent> entity, ref RuleLoadedGridsEvent e)
    {
        var query1 = AllEntityQuery<MapGridComponent>();
        while (query1.MoveNext(out var ent, out var shuttle))
        {
            EnsureComp<ExtractionShuttleComponent>(ent);
        }

        var query = AllEntityQuery<ExtractionShuttleComponent>();
        while (query.MoveNext(out _, out var shuttle))
        {
            entity.Comp.Shuttle = shuttle;

            return;
        }
    }
}

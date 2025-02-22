using System.Linq;
using System.Text;
using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Server.Shuttles.Events;
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
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoxRaidersRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
        SubscribeLocalEvent<VoxRaidersRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);

        SubscribeLocalEvent<VoxRaidersShuttleComponent, FTLCompletedEvent>(OnFTLCompleted);
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
        StringBuilder builder = new();

        builder.AppendLine(Loc.GetString(component.Success ? "vox-raiders-success" : "vox-raiders-fail"));
        builder.AppendLine(Loc.GetString("vox-raiders-objectives"));

        foreach (var objectives in component.Objectives.Values)
        {
            if (objectives.Count < 1)
                continue;

            var info = _objectives.GetInfo(objectives[0].Objective, objectives[0].Mind, objectives[0].Mind);

            if (info is null)
                continue;

            builder.AppendLine(info.Value.Title);
        }

        args.AddLine(builder.ToString());
    }

    private void OnAfterAntagEntitySelected(Entity<VoxRaidersRuleComponent> entity, ref AfterAntagEntitySelectedEvent e)
    {
        var mind = e.Session?.GetMind();

        if (mind is null)
            return;

        var query = AllEntityQuery<VoxRaidersShuttleComponent, ExtractionShuttleComponent>();
        while (query.MoveNext(out var shuttle, out var extraction))
            if (shuttle.Rule == entity.Owner)
                extraction.Owners.Add(mind.Value);

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
        while (query1.MoveNext(out var ent, out _))
        {
            if (Transform(ent).MapID != e.Map)
                continue;

            EnsureComp<VoxRaidersShuttleComponent>(ent);
        }

        entity.Comp.Map = _map.GetMap(e.Map);

        var query = AllEntityQuery<VoxRaidersShuttleComponent>();
        while (query.MoveNext(out var ent, out var shuttle))
        {
            if (Transform(ent).MapID != e.Map)
                continue;

            EnsureComp<ExtractionShuttleComponent>(ent);

            shuttle.Rule = entity;
        }
    }

    private void OnFTLCompleted(Entity<VoxRaidersShuttleComponent> entity, ref FTLCompletedEvent e)
    {
        if (!TryComp<VoxRaidersRuleComponent>(entity.Comp.Rule, out var rule))
            return;

        if (e.MapUid != rule.Map)
            return;

        foreach (var objectives in rule.Objectives.Values)
            if (!objectives.All(objective => _objectives.IsCompleted(objective.Objective, objective.Mind)))
                return;

        Del(e.MapUid);

        rule.Success = true;
    }
}

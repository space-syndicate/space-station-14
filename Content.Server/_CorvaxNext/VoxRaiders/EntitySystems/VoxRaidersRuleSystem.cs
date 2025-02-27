using System.Text;
using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Server.Objectives.Components.Targets;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Random.Helpers;
using Content.Shared.Shuttles.Components;
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
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoxRaidersRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
        SubscribeLocalEvent<VoxRaidersRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);

        SubscribeLocalEvent<VoxRaidersPinpointerComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
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

            var info = _objectives.GetInfo(objectives[0].Objective, objectives[0].Mind);

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

        entity.Comp.Raiders.Add(e.EntityUid);

        var query = AllEntityQuery<VoxRaidersMapComponent, ExtractionMapComponent>();
        while (query.MoveNext(out var map, out var extraction))
            if (map.Rule == entity.Owner)
                extraction.Owners.Add(mind.Value);

        foreach (var objective in entity.Comp.ObjectivePrototypes)
        {
            if (!TryComp<MindComponent>(mind.Value, out var mindComponent))
                continue;

            var obj = _objectives.TryCreateObjective(mind.Value, mindComponent, objective);

            if (obj is null)
                continue;

            _mind.AddObjective(mind.Value, mindComponent, obj.Value);

            entity.Comp.Objectives.GetOrNew(objective).Add((obj.Value, mind.Value));
        }
    }

    private void OnRuleLoadedGrids(Entity<VoxRaidersRuleComponent> entity, ref RuleLoadedGridsEvent e)
    {
        var map = _map.GetMap(e.Map);

        var voxRaidersMap = EnsureComp<VoxRaidersMapComponent>(map);

        voxRaidersMap.Rule = entity;

        EnsureComp<ExtractionMapComponent>(map);

        _shuttle.TryAddFTLDestination(e.Map, true, out _);

        var query = AllEntityQuery<ShuttleConsoleComponent, ItemSlotsComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out _, out var slots, out var transform))
        {
            if (transform.MapID != e.Map)
                continue;

            var disk = Spawn("CoordinatesDisk");

            var destination = EnsureComp<ShuttleDestinationCoordinatesComponent>(disk);

            destination.Destination = map;

            Dirty(disk, destination);

            _slots.TryInsert(ent, SharedShuttleConsoleComponent.DiskSlotName, disk, null, slots);
        }
    }

    private void OnMapInit(Entity<VoxRaidersPinpointerComponent> entity, ref MapInitEvent e)
    {
        if (!TryComp<VoxRaidersMapComponent>(Transform(entity).MapUid, out var map))
            return;

        if (!TryComp<VoxRaidersRuleComponent>(map.Rule, out var rule))
            return;

        if (!TryComp<ControlPinpointerComponent>(entity, out var pin))
            return;

        foreach (var raider in rule.Raiders)
            pin.Entities.Add(raider);

        foreach (var objectives in rule.Objectives.Values)
        {
            if (!TryComp<ExtractConditionComponent>(objectives[0].Objective, out var condition))
                continue;

            var query = AllEntityQuery<StealTargetComponent>();
            while (query.MoveNext(out var ent, out var target))
            {
                if (target.StealGroup != condition.StealGroup)
                    continue;

                pin.Entities.Add(ent);

                break;
            }
        }
    }

    private void OnFTLCompleted(ref FTLCompletedEvent e)
    {
        if (!TryComp<VoxRaidersMapComponent>(e.MapUid, out var map))
            return;

        if (!TryComp<VoxRaidersRuleComponent>(map.Rule, out var rule))
            return;

        foreach (var objectives in rule.Objectives.Values)
            foreach (var objective in objectives)
                if (TryComp<MindComponent>(objective.Mind, out var mind))
                    if (!_objectives.IsCompleted(objective.Objective, (objective.Mind, mind)))
                        return;

        foreach (var raider in rule.Raiders)
        {
            if (!TryComp<ExtractionMapComponent>(Transform(raider).MapUid, out var extraction))
                return;

            if (!_mind.TryGetMind(raider, out var mind, out _))
                return;

            if (!extraction.Owners.Contains(mind))
                return;
        }

        rule.Success = true;
    }
}

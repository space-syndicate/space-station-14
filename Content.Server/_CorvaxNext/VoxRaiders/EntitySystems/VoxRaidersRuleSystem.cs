using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Players;
using Robust.Shared.Map.Components;

namespace Content.Server._CorvaxNext.VoxRaiders.EntitySystems;

public sealed class VoxRaidersRuleSystem : GameRuleSystem<VoxRaidersRuleComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoxRaidersRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
        SubscribeLocalEvent<VoxRaidersRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
    }

    protected override void AppendRoundEndText(EntityUid uid, VoxRaidersRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        
    }

    private void OnAfterAntagEntitySelected(Entity<VoxRaidersRuleComponent> entity, ref AfterAntagEntitySelectedEvent e)
    {
        var mind = e.Session?.GetMind();

        if (mind is null)
            return;

        entity.Comp.Shuttle?.Owners.Add(mind.Value);
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

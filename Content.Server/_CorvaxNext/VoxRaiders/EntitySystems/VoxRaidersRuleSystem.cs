using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Mind;

namespace Content.Server._CorvaxNext.VoxRaiders.EntitySystems;

public sealed class VoxRaidersRuleSystem : GameRuleSystem<VoxRaidersRuleComponent>
{
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoxRaidersRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
    }

    private void OnAfterAntagEntitySelected(Entity<VoxRaidersRuleComponent> entity, ref AfterAntagEntitySelectedEvent e)
    {
        //_mind.TryAddObjective();
    }
}

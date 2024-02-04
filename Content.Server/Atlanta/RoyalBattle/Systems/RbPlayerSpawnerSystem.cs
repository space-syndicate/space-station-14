using Content.Server.Atlanta.GameTicking.Rules;
using Content.Server.Atlanta.GameTicking.Rules.Components;
using Content.Server.Atlanta.RoyalBattle.Components;

namespace Content.Server.Atlanta.RoyalBattle.Systems;

/// <summary>
/// This handles...
/// </summary>
public sealed class RbPlayerSpawnerSystem : EntitySystem
{
    /// <inheritdoc/>

    public override void Initialize()
    {
        SubscribeLocalEvent<RbPlayerSpawnerComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(EntityUid uid, RbPlayerSpawnerComponent component, ComponentStartup args)
    {
        var query = EntityQueryEnumerator<RoyalBattleRuleComponent>();

        while (query.MoveNext(out var rule))
        {
            RoyalBattleRuleSystem.AddSpawner(rule, uid);
        }
    }
}

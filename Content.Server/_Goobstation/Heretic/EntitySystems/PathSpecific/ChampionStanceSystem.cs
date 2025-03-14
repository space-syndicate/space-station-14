using Content.Server.Heretic.Components.PathSpecific;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;

namespace Content.Server.Heretic.EntitySystems.PathSpecific;

public sealed partial class ChampionStanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChampionStanceComponent, DamageModifyEvent>(OnDamageModify);

        // if anyone is reading through and does not have EE newmed you can remove these handlers
    }

    private bool Condition(Entity<ChampionStanceComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var dmg)
        || dmg.TotalDamage < 50f) // taken that humanoids have 100 damage before critting
            return false;
        return true;
    }

    private void OnDamageModify(Entity<ChampionStanceComponent> ent, ref DamageModifyEvent args)
    {
        if (!Condition(ent))
            return;

        args.Damage = args.OriginalDamage / 2f;
    }
}

using Content.Shared._CorvaxNext.TurretControl.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Verbs;

namespace Content.Shared._CorvaxNext.TurretControl;

public sealed class TurretControlSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string Passive = "TurretPassive";

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string Peace = "TurretPeace";

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string Hostile = "TurretHostile";

    public override void Initialize()
    {
        SubscribeLocalEvent<TurretControllableComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<TurretControllableComponent> entity, ref GetVerbsEvent<Verb> e)
    {
        if (!TryComp<TurretControllerComponent>(e.User, out var controller))
            return;

        foreach (var component in controller.RequiredComponents)
            if (!HasComp(e.User, component.Value.Component.GetType()))
                return;

        if (!TryComp<NpcFactionMemberComponent>(entity, out var factionMember))
            return;

        e.Verbs.Add(CreateVerb((entity, factionMember), "turret-control-mode-nobody", Passive, 3));
        e.Verbs.Add(CreateVerb((entity, factionMember), "turret-control-mode-hostile", Peace, 2));
        e.Verbs.Add(CreateVerb((entity, factionMember), "turret-control-mode-everybody", Hostile, 1));
    }

    private Verb CreateVerb(Entity<NpcFactionMemberComponent?> entity, string text, string faction, int priority)
    {
        return new()
        {
            Text = Loc.GetString(text),
            Disabled = _faction.IsMember(entity, faction),
            Category = VerbCategory.TurretControlMode,
            Priority = priority,
            Act = () => SetFaction(entity, faction)
        };
    }

    private void SetFaction(Entity<NpcFactionMemberComponent?> entity, string faction)
    {
        _faction.RemoveFaction(entity, Passive);
        _faction.RemoveFaction(entity, Peace);
        _faction.RemoveFaction(entity, Hostile);

        _faction.AddFaction(entity, faction);
    }
}

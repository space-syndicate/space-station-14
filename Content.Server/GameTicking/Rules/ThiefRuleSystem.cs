using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Shared.Humanoid;
using Content.Shared.Roles;

namespace Content.Server.GameTicking.Rules;

public sealed class ThiefRuleSystem : GameRuleSystem<ThiefRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThiefRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);

        SubscribeLocalEvent<ThiefRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    // Greeting upon thief activation
    private void AfterAntagSelected(Entity<ThiefRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;
        _antag.SendBriefing(ent, MakeBriefing(ent, args.Def.PrefRoles.Contains("Api")), null, null); // Corvax-Next-Api
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<ThiefRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;

        // Corvax-Next-Api-Start
        var api = false;

        foreach (var id in args.Mind.Comp.MindRoles)
            if (TryComp<MindRoleComponent>(id, out var mindRole))
                if (mindRole.AntagPrototype == "Api")
                    api = true;

        args.Append(MakeBriefing(ent.Value, api));
        // Corvax-Next-Api-End
    }

    private string MakeBriefing(EntityUid ent, bool api) // Corvax-Next-Api
    {
        // Corvax-Next-Api-Start
        if (api)
            return Loc.GetString("api-role-greeting");
        // Corvax-Next-Api-End

        var isHuman = HasComp<HumanoidAppearanceComponent>(ent);
        var briefing = isHuman
            ? Loc.GetString("thief-role-greeting-human")
            : Loc.GetString("thief-role-greeting-animal");

        if (isHuman)
            briefing += "\n \n" + Loc.GetString("thief-role-greeting-equipment") + "\n";

        return briefing;
    }
}

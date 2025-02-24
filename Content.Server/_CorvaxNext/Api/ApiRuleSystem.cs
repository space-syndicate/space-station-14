using Content.Server._CorvaxNext.Api.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;

namespace Content.Server._CorvaxNext.Api;

public sealed class ApiRuleSystem : GameRuleSystem<ApiRuleComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ApiRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnGetBriefing(Entity<ApiRoleComponent> entity, ref GetBriefingEvent e)
    {
        e.Append(Loc.GetString("api-role-greeting"));
    }
}

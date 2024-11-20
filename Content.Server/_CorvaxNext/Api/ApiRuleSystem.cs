using Content.Server._CorvaxNext.Api.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;

namespace Content.Server._CorvaxNext.Api;

public sealed class ApiRuleSystem : GameRuleSystem<ApiRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ApiRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
        SubscribeLocalEvent<ApiRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnAfterAntagEntitySelected(EntityUid entity, ApiRuleComponent apiRule, AfterAntagEntitySelectedEvent e)
    {
        _antag.SendBriefing(e.EntityUid, Loc.GetString("api-role-greeting"), null, null);
    }

    private void OnGetBriefing(EntityUid entity, ApiRoleComponent apiRole, GetBriefingEvent e)
    {
        e.Append(Loc.GetString("api-role-greeting"));
    }
}

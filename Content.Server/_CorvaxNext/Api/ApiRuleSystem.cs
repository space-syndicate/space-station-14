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
        SubscribeLocalEvent<ApiRuleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnAfterAntagEntitySelected(EntityUid entity, ApiRuleComponent apiRule, AfterAntagEntitySelectedEvent e)
    {
        _antag.SendBriefing(e.EntityUid, Loc.GetString("api-role-greeting"), null, null);
    }

    private void OnGetBriefing(EntityUid entity, ApiRuleComponent apiRule, GetBriefingEvent e)
    {
        e.Append(Loc.GetString("api-role-greeting"));
    }
}

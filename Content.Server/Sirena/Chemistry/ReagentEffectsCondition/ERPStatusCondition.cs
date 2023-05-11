using Content.Server.Chemistry.ReactionEffects;
using Content.Server.Database;
using Content.Server.DetailExaminable;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Sirena;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Content.Server.Sirena.Chemistry.ReagentEffectsCondition;

public sealed class ERPStatusCondition : ReagentEffectCondition
{
    [DataField("erp")]
    public EnumERPStatus ERP = default!;

    [DataField("shouldHave")]
    public bool ShouldHave = true;

    public override bool Condition(ReagentEffectArgs args)
    {

        EnumERPStatus enterp;
        var hasCom = args.EntityManager.HasComponent<DetailExaminableComponent>(args.SolutionEntity);
        if (hasCom == true)
        enterp = args.EntityManager.GetComponent<DetailExaminableComponent>(args.SolutionEntity).ERPStatus;
        else
            return false;
        if (enterp == ERP)
            return ShouldHave;
        else
            return !ShouldHave;

    }
}

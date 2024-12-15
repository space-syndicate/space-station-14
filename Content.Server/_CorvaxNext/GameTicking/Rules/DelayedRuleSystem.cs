using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules;
using Content.Server._CorvaxNext.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Popups;

namespace Content.Server._CorvaxNext.GameTicking.Rules;

public sealed class DelayedRuleSystem : GameRuleSystem<DelayedRuleComponent>
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected override void Started(EntityUid uid, DelayedRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.DelayEnds = Timing.CurTime + component.Delay;
    }

    protected override void ActiveTick(EntityUid uid, DelayedRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        CheckDelay((uid, component));
    }

    /// <summary>
    /// Checks if the delay has ended.
    /// </summary>
    private void CheckDelay(Entity<DelayedRuleComponent> ent)
    {
        if (!TryComp<AntagSelectionComponent>(ent, out var selection))
            return;

        // skip the delay if it's just 1 player, theres no plan to ruin if you are the only one
        var ends = ent.Comp.DelayEnds;
        if (ent.Comp.IgnoreSolo && selection.SelectedMinds.Count < 2)
            ends = Timing.CurTime;

        if (Timing.CurTime < ends)
            return;

        var comps = ent.Comp.DelayedComponents;
        foreach (var (mindId, _) in selection.SelectedMinds)
        {
            // using OriginalOwnedEntity as the player might have ghosted to try become an evil ghost antag
            if (!TryComp<MindComponent>(mindId, out var mind) || !TryGetEntity(mind.OriginalOwnedEntity, out var mob))
                continue;

            var uid = mob.Value;
            _popup.PopupEntity(Loc.GetString(ent.Comp.EndedPopup), uid, uid, PopupType.LargeCaution);
            EntityManager.AddComponents(uid, comps);
        }

        RemCompDeferred<DelayedRuleComponent>(ent);
    }
}
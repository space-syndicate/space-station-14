using Content.Shared.Actions;
using Content.Server.Mind;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;
using Content.Shared.Mind.Components;
using Content.Shared.NPC;
using Content.Shared.SSDIndicator;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class MindSwapPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    #if !DEBUG
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    #endif

    private ISawmill _logger = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logger = Logger.GetSawmill("mindswap");
    }

    public bool Swap(EntityUid performer, EntityUid target, bool end = false)
    {
        if (performer == target)
        {
            return false;
        }
        if (end && (!HasComp<Abilities.MindSwappedComponent>(performer) || !HasComp<Abilities.MindSwappedComponent>(target)))
        {
            return false;
        }
        if (!end)
        {
            if (HasComp<Abilities.MindSwappedComponent>(performer))
            {
                _popupSystem.PopupCursor("Ошибка! Вы уже в другом теле!", performer);
                return false; // Повторный свап!? TODO: chain swap, in current mode broken chained in no return (has no mind error)
            }

            if (HasComp<Abilities.MindSwappedComponent>(target))
            {
                _popupSystem.PopupCursor("Ошибка! Ваша цель уже в другом теле!", performer);
                return false; // Повторный свап!? TODO: chain swap, in current mode broken chained in no return (has no mind error)
            }

            if (HasComp<ActiveNPCComponent>(performer) || HasComp<ActiveNPCComponent>(target))
            {
                _popupSystem.PopupCursor("Ошибка! Ваша цель в ссд!", performer);
                return false;
            }
        }
        // This is here to prevent missing MindContainerComponent Resolve errors.
        var a = _mindSystem.TryGetMind(performer, out var performerMindId, out var performerMind);
        var b = _mindSystem.TryGetMind(target, out var targetMindId, out var targetMind);


        _logger.Info($"swap performer: {ToPrettyString(performer):Entity} target: {ToPrettyString(target):Entity}");

        ICommonSession? performerSession = null;
        ICommonSession? targetSession = null;

        if (a)
        {
            performerSession = performerMind!.Session;
            _mindSystem.TransferTo(performerMindId, null, true);
        }

        if (b)
        {
            targetSession = targetMind!.Session;
            _mindSystem.TransferTo(targetMindId, null, true);
        }

        // Do the transfer.
        if (a)
        {
            RemComp<ActorComponent>(target);
            RemComp<MindContainerComponent>(target);
            //_mindSystem.SetUserId(performerMindId, performerMind!.UserId, performerMind);
            var isSsd = performerSession == null;

            _mindSystem.TransferTo(performerMindId, target, true, false);
            Timer.Spawn(1_000, () =>
            {
                if (!target.IsValid() || !TryComp<SSDIndicatorComponent>(target, out var ssd))
                    return;
                ssd.IsSSD = isSsd;
                Dirty(target,ssd);
            });

        }


        if (b)
        {

            RemComp<ActorComponent>(performer);
            RemComp<MindContainerComponent>(performer);
            //_mindSystem.SetUserId(targetMindId, targetMind!.UserId, targetMind);
            var isSsd = targetSession == null;
            _mindSystem.TransferTo(targetMindId, performer, true, false);
            Timer.Spawn(1_000, () =>
            {
                if (!performer.IsValid() || !TryComp<SSDIndicatorComponent>(performer, out var ssd))
                    return;
                ssd.IsSSD = isSsd;
                Dirty(performer,ssd);
            });

        }

        if (end)
        {
            if (TryComp<Abilities.MindSwappedComponent>(performer, out var mindSwapCompP))
            {
                _actions.RemoveAction(performer,  mindSwapCompP.MindSwapReturn);
            }
            if (TryComp<Abilities.MindSwappedComponent>(target, out var mindSwapCompT))
            {
                _actions.RemoveAction(target, mindSwapCompT.MindSwapReturn);
            }

            RemComp<Abilities.MindSwappedComponent>(performer);
            RemComp<Abilities.MindSwappedComponent>(target);

            return true;
        }

        var perfComp = EnsureComp<Abilities.MindSwappedComponent>(performer);
        var targetComp = EnsureComp<Abilities.MindSwappedComponent>(target);

        perfComp.OriginalEntity = target;
        perfComp.OriginalMindId = targetMindId;
        targetComp.OriginalEntity = performer;
        targetComp.OriginalMindId = performerMindId;

        return true;
    }
}

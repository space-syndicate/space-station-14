using Content.Server.Medical.Components;
using Content.Server.Popups;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Shared.Medical;
using YamlDotNet.Core.Tokens;

namespace Content.Server.Medical
{
    public sealed class MedicalDrapesSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        public override void Initialize()
        {
            SubscribeLocalEvent<MedicalDrapesComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<MedicalDrapesComponent, MedicalDrapesDoAfterEvent>(OnDoAfter);
        }

        private void OnAfterInteract(Entity<MedicalDrapesComponent> uid, ref AfterInteractEvent args)
        {
            if (args.Target == null || !args.CanReach || !HasComp<MobStateComponent>(args.Target))
                return;

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, uid.Comp.UseDelay, new MedicalDrapesDoAfterEvent(), uid, target: args.Target, used: uid)
            {
                NeedHand = true,
                BreakOnMove = true
            });
        }

        private void OnDoAfter(Entity<MedicalDrapesComponent> uid, ref MedicalDrapesDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Target == null)
                return;
            args.Handled = true;
            EntityUid target = (EntityUid)args.Target;
            TryComp<BuckleComponent>(args.Target, out var buckle);
            var ent = buckle!.BuckledTo;
            TryComp<ActiveSurgeryComponent>(args.Target, out var surgery);
            if (!HasComp<ActiveSurgeryComponent>(target))
            {
                if (target == args.User)
                    return;
                if(!buckle.Buckled)
                {
                    _popupSystem.PopupEntity(Loc.GetString("medical-surgery-cantoperate"), uid, args.User);
                    return;
                }
                AddComp<ActiveSurgeryComponent>(target);
                _popupSystem.PopupEntity(Loc.GetString("medical-surgery-activate"), uid, args.User);
            }
            if (HasComp<ActiveSurgeryComponent>(target) && surgery!.IsActive != true)
            {
                if (target == args.User)
                    return;
                RemComp<ActiveSurgeryComponent>(target);
                _popupSystem.PopupEntity(Loc.GetString("medical-surgery-stop"), uid, args.User);
            }

        }
    }
}

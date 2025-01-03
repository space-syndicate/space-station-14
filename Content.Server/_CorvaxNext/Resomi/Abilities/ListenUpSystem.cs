using Content.Shared._CorvaxNext.Resomi.Abilities.Hearing;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.IdentityManagement;

namespace Content.Server._CorvaxNext.Resomi.Abilities;

public sealed class ListenUpSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ListenUpComponent, ComponentStartup>(OnListenStartup);
    }
    private void OnListenStartup(Entity<ListenUpComponent> ent, ref ComponentStartup args)
    {
        _popup.PopupEntity(Loc.GetString("listen-up-activated-massage", ("name", Identity.Entity(ent.Owner, EntityManager))), ent.Owner);
    }
}

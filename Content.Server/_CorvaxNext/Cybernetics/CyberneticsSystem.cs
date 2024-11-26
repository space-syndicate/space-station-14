using Content.Server.Emp;
using Content.Shared._CorvaxNext.Surgery.Body;
using Content.Shared._CorvaxNext.Surgery.Body.Events;
using Content.Shared._CorvaxNext.Surgery.Body.Organs;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;

namespace Content.Server._CorvaxNext.Cybernetics;

internal sealed class CyberneticsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CyberneticsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CyberneticsComponent, EmpDisabledRemoved>(OnEmpDisabledRemoved);
    }
    private void OnEmpPulse(Entity<CyberneticsComponent> cyberEnt, ref EmpPulseEvent ev)
    {
        if (!cyberEnt.Comp.Disabled)
        {
            ev.Affected = true;
            ev.Disabled = true;
            cyberEnt.Comp.Disabled = true;

            if (HasComp<OrganComponent>(cyberEnt))
            {
                var disableEvent = new OrganEnableChangedEvent(false);
                RaiseLocalEvent(cyberEnt, ref disableEvent);
            }
            else if (HasComp<BodyPartComponent>(cyberEnt))
            {
                var disableEvent = new BodyPartEnableChangedEvent(false);
                RaiseLocalEvent(cyberEnt, ref disableEvent);
            }
        }
    }

    private void OnEmpDisabledRemoved(Entity<CyberneticsComponent> cyberEnt, ref EmpDisabledRemoved ev)
    {
        if (cyberEnt.Comp.Disabled)
        {
            cyberEnt.Comp.Disabled = false;
            if (HasComp<OrganComponent>(cyberEnt))
            {
                var enableEvent = new OrganEnableChangedEvent(true);
                RaiseLocalEvent(cyberEnt, ref enableEvent);
            }
            else if (HasComp<BodyPartComponent>(cyberEnt))
            {
                var enableEvent = new BodyPartEnableChangedEvent(true);
                RaiseLocalEvent(cyberEnt, ref enableEvent);
            }
        }
    }
}

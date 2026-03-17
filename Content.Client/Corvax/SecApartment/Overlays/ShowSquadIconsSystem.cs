using Content.Shared.Overlays;
using Content.Shared.SecApartment;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed class ShowSquadIconsSystem : EquipmentHudSystem<ShowSquadIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SquadMemberComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, SquadMemberComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_prototype.Resolve(component.StatusIcon, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}

using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.SecApartment;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SquadMemberComponent : Component
{
    /// <summary>
    ///     The icon that should be displayed based on the squad icon of the entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SecurityIconPrototype> StatusIcon = "SecuritySquadIconAlpha";
}

using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared.Backmen.EntityHealthBar;

/// <summary>
/// This component allows you to see health bars above damageable mobs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BkmShowHealthBarsComponent : Component
{
    /// <summary>
    /// If null, displays all health bars.
    /// If not null, displays health bars of only that damage container.
    /// </summary>

    [AutoNetworkedField]
    [DataField("damageContainers", customTypeSerializer: typeof(PrototypeIdListSerializer<DamageContainerPrototype>))]
    public List<string> DamageContainers = new();

    public override bool SendOnlyToOwner => true;
}

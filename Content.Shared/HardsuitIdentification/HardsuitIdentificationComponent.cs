using Content.Shared.Actions.ActionTypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.HardsuitIdentification;

[NetworkedComponent, RegisterComponent]
public sealed class HardsuitIdentificationComponent : Component
{
    [DataField("faction", customTypeSerializer: typeof(PrototypeIdSerializer<InstantActionPrototype>))]
    public string Faction = string.Empty;

    public bool Activated = false;
}

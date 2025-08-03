using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Corvax.Events.Components
{
    /// <summary>
    /// Component for entities that want to get an event icon.
    /// </summary>
    [NetworkedComponent, RegisterComponent]
    public sealed partial class EventIconComponent : Component
    {
        [DataField("eventStatusIcon")]
        public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "EventFaction";
    }

    /// <summary>
    /// Component for entities that want to show an event icon.
    /// </summary>
    [NetworkedComponent, RegisterComponent]
    public sealed partial class ShowEventIconComponent : Component;
}

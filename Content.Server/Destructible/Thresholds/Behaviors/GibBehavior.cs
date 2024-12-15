using Content.Shared.Body.Components;
using Content.Shared.Gibbing.Events;
using JetBrains.Annotations;

namespace Content.Server.Destructible.Thresholds.Behaviors
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class GibBehavior : IThresholdBehavior
    {
        [DataField] public GibType GibType = GibType.Gib; // CorvaxNext: surgery
        [DataField] public GibContentsOption GibContents = GibContentsOption.Drop; // CorvaxNext: surgery
        [DataField("recursive")] private bool _recursive = true;

        public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        {
            if (system.EntityManager.TryGetComponent(owner, out BodyComponent? body))
            {
                system.BodySystem.GibBody(owner, _recursive, body, gib: GibType, contents: GibContents);
            }
        }
    }
}

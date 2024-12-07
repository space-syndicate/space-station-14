using Content.Shared.Examine;
using Robust.Server.Containers;

namespace Content.Server.Corvax.HiddenDescription;

public sealed class HiddenDescriptionContainerShowerSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly HiddenDescriptionSystem _hiddenDescription = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiddenDescriptionContainerShowerComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<HiddenDescriptionContainerShowerComponent> entity, ref ExaminedEvent args)
    {
        foreach (var container in _container.GetAllContainers(entity.Owner))
        {
            foreach (var containedEntity in container.ContainedEntities)
            {
                if (TryComp<HiddenDescriptionComponent>(containedEntity, out var hiddenDescription))
                {
                    _hiddenDescription.PushExamineInformation(hiddenDescription, ref args);
                }
            }
        }
    }
}

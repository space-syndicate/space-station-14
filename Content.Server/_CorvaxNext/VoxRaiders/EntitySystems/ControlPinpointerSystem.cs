using Content.Server._CorvaxNext.VoxRaiders.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Verbs;

namespace Content.Server._CorvaxNext.VoxRaiders.EntitySystems;

public sealed class ControlPinpointerSystem : EntitySystem
{
    [Dependency] private readonly SharedPinpointerSystem _pin = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ControlPinpointerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<ControlPinpointerComponent> entity, ref GetVerbsEvent<Verb> e)
    {
        if (!TryComp<PinpointerComponent>(entity, out var pin))
            return;

        var index = 0;

        foreach (var ent in entity.Comp.Entities)
            e.Verbs.Add(new()
            {
                Text = MetaData(ent).EntityName,
                Disabled = pin.Target == ent,
                Category = VerbCategory.PinpointerTarget,
                Priority = index--,
                Act = () => _pin.SetTarget(entity, ent, pin)
            });
    }
}

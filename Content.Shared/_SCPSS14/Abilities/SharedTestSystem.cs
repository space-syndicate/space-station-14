using Content.Shared.Actions;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._SCPSS14.Abilities;

public abstract class SharedTestSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TestComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(EntityUid uid, TestComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, ref component.Action, component.TestAction, uid);
    }
}

using System.Linq;
using Content.Server.Popups;
using Content.Shared._SCPSS14.Abilities;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._SCPSS14.Abilities;

public sealed class TestSystem : SharedTestSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TestComponent, TestActionEvent>(OnSpawnEvent);
    }

    private void OnSpawnEvent(EntityUid uid, TestComponent component, TestActionEvent args)
    {
        if (args.Handled)
            return;

        var transform = Transform(uid);

        if (transform.GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("nogrid"), args.Performer, args.Performer);
            return;
        }
        var coords = transform.Coordinates;
        Spawn(component.SpawnPrototype, coords);
        args.Handled = true;
    }
}


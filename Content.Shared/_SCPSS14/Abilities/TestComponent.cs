using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._SCPSS14.Abilities;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedTestSystem))]
public sealed partial class TestComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("spawnPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string SpawnPrototype = "WallSolid";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("testAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string TestAction = "ActionTestSpawnPrototype";

    [DataField] public EntityUid? Action;
}

public sealed partial class TestActionEvent : InstantActionEvent { }

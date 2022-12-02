using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Corvax.OwOAction;

[RegisterComponent]
public sealed class OwOActionComponent : Component
{
    [DataField("actionId", customTypeSerializer:typeof(PrototypeIdSerializer<InstantActionPrototype>))]
    public string ActionId = "OwOVoice";

    [DataField("action")] // must be a data-field to properly save cooldown when saving game state.
    public InstantAction? OwOAction = null;
}

public sealed class OwOAccentActionEvent : InstantActionEvent { };

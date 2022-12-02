using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Corvax.OwOAction;

[RegisterComponent]
public sealed class OwOActionComponent : Component
{
    private bool _isON;

    [DataField("actionId", customTypeSerializer:typeof(PrototypeIdSerializer<InstantActionPrototype>))]
    public string ActionId = "OwOVoice";

    [DataField("action")] // must be a data-field to properly save cooldown when saving game state.
    public InstantAction? OwOAction = null;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsON
    {
        get => _isON;
        set
        {
            if(_isON == value) return;
            _isON = value;
            if(OwOAction != null)
                EntitySystem.Get<SharedActionsSystem>().SetToggled(OwOAction, _isON);

            Dirty();
        }
    }
}

public sealed class OwOAccentActionEvent : InstantActionEvent { };

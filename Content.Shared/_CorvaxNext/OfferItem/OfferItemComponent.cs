using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxNext.OfferItem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedOfferItemSystem))]
public sealed partial class OfferItemComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public bool IsInOfferMode;

    [DataField, AutoNetworkedField]
    public bool IsInReceiveMode;

    [DataField, AutoNetworkedField]
    public string? Hand;

    [DataField, AutoNetworkedField]
    public EntityUid? Item;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField]
    public float MaxOfferDistance = 2f;
}

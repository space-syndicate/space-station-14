using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._CorvaxNext.Cards.Card;

/// <summary>
/// Represents a card entity that has both a front and back sprite, and can be flipped. 
/// This component is networked and supports updating the state of the card's visuals and name across the network.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardComponent : Component
{
    /// <summary>
    /// The back of the card
    /// </summary>
    [DataField("backSpriteLayers", readOnly: true)]
    public List<SpriteSpecifier>? BackSprite = [];

    /// <summary>
    /// The front of the card
    /// </summary>
    [DataField("frontSpriteLayers", readOnly: true)]
    public List<SpriteSpecifier> FrontSprite = [];

    /// <summary>
    /// If it is currently flipped. This is used to update sprite and name.
    /// </summary>
    [DataField("flipped", readOnly: true), AutoNetworkedField]
    public bool Flipped = false;


    /// <summary>
    /// The name of the card.
    /// </summary>
    [DataField("name", readOnly: true), AutoNetworkedField]
    public string Name = "";

}

[Serializable, NetSerializable]
public sealed class CardFlipUpdatedEvent(NetEntity card) : EntityEventArgs
{
    public NetEntity Card = card;
}

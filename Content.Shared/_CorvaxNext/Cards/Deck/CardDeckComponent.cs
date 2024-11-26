using Robust.Shared.Audio;

namespace Content.Shared._CorvaxNext.Cards.Deck;

/// <summary>
/// Represents a deck of cards with configurable properties such as sound effects for interactions,
/// visual scaling, and a limit on the number of cards it can hold.
/// </summary>
[RegisterComponent]
public sealed partial class CardDeckComponent : Component
{
    [DataField("shuffleSound")]
    public SoundSpecifier ShuffleSound = new SoundCollectionSpecifier("cardFan");

    [DataField("pickUpSound")]
    public SoundSpecifier PickUpSound = new SoundCollectionSpecifier("cardSlide");

    [DataField("placeDownSound")]
    public SoundSpecifier PlaceDownSound = new SoundCollectionSpecifier("cardShove");

    [DataField("yOffset")]
    public float YOffset = 0.02f;

    [DataField("scale")]
    public float Scale = 1;

    [DataField("limit")]
    public int CardLimit = 5;
}

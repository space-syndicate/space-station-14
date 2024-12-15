using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Cards.Hand;

/// <summary>
/// Represents a hand of cards with configurable properties such as the arrangement angle,
/// offsets, scaling, card limit, and whether the cards are flipped.
/// </summary>
[RegisterComponent]
public sealed partial class CardHandComponent : Component
{
    [DataField("angle")]
    public float Angle = 120f;

    [DataField("xOffset")]
    public float XOffset = 0.5f;

    [DataField("scale")]
    public float Scale = 1;

    [DataField("limit")]
    public int CardLimit = 10;

    [DataField("flipped")]
    public bool Flipped = false;
}


[Serializable, NetSerializable]
public enum CardUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CardHandDrawMessage(NetEntity card) : BoundUserInterfaceMessage
{
    public NetEntity Card = card;
}

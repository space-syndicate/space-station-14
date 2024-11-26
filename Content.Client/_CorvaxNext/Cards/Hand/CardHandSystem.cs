using System.Numerics;
using Content.Shared._CorvaxNext.Cards.Hand;
using Content.Shared._CorvaxNext.Cards.Stack;
using Robust.Client.GameObjects;

namespace Content.Client._CorvaxNext.Cards.Hand;

/// <summary>
/// Handles the visual representation and sprite updates for the player's hand of cards on the client side.
/// Responds to events related to the card stack to update the card hand's appearance accordingly.
/// </summary>
public sealed class CardHandSystem : EntitySystem
{
    [Dependency] private readonly CardSpriteSystem _cardSpriteSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardHandComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<CardStackInitiatedEvent>(OnStackStart);
        SubscribeNetworkEvent<CardStackQuantityChangeEvent>(OnStackUpdate);
        SubscribeNetworkEvent<CardStackReorderedEvent>(OnStackReorder);
        SubscribeNetworkEvent<CardStackFlippedEvent>(OnStackFlip);
    }

    private void UpdateSprite(EntityUid uid, CardHandComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<CardStackComponent>(uid, out var cardStack))
            return;

        if (!_cardSpriteSystem.TryAdjustLayerQuantity((uid, sprite, cardStack), comp.CardLimit))
            return;

        var cardCount = Math.Min(cardStack.Cards.Count, comp.CardLimit);

        if (cardCount <= 1)
        {
            // Single card case
            _cardSpriteSystem.TryHandleLayerConfiguration(
                (uid, sprite, cardStack),
                cardCount,
                (spriteEntity, cardIndex, layerIndex) =>
                {
                    spriteEntity.Comp.LayerSetRotation(layerIndex, Angle.Zero);
                    spriteEntity.Comp.LayerSetOffset(layerIndex, new Vector2(0, 0.10f));
                    spriteEntity.Comp.LayerSetScale(layerIndex, new Vector2(comp.Scale, comp.Scale));
                    return true;
                }
            );
        }
        else
        {
            // Multiple cards case
            var intervalAngle = comp.Angle / (cardCount - 1);
            var intervalSize = comp.XOffset / (cardCount - 1);

            _cardSpriteSystem.TryHandleLayerConfiguration(
                (uid, sprite, cardStack),
                cardCount,
                (spriteEntity, cardIndex, layerIndex) =>
                {
                    var angle = -(comp.Angle / 2) + cardIndex * intervalAngle;
                    var xOffset = -(comp.XOffset / 2) + cardIndex * intervalSize;
                    var yOffset = -(xOffset * xOffset) + 0.10f;

                    spriteEntity.Comp.LayerSetRotation(layerIndex, Angle.FromDegrees(-angle));
                    spriteEntity.Comp.LayerSetOffset(layerIndex, new Vector2(xOffset, yOffset));
                    spriteEntity.Comp.LayerSetScale(layerIndex, new Vector2(comp.Scale, comp.Scale));
                    return true;
                }
            );
        }
    }

    private void OnStackUpdate(CardStackQuantityChangeEvent args)
    {
        var entity = GetEntity(args.Stack);
        if (!TryComp<CardHandComponent>(entity, out var comp))
            return;

        UpdateSprite(entity, comp);
    }

    private void OnStackStart(CardStackInitiatedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp<CardHandComponent>(entity, out var comp))
            return;

        UpdateSprite(entity, comp);
    }

    private void OnComponentStartupEvent(EntityUid uid, CardHandComponent comp, ComponentStartup args)
    {
        UpdateSprite(uid, comp);
    }

    private void OnStackReorder(CardStackReorderedEvent args)
    {
        var entity = GetEntity(args.Stack);
        if (!TryComp<CardHandComponent>(entity, out var comp))
            return;

        UpdateSprite(entity, comp);
    }

    private void OnStackFlip(CardStackFlippedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp<CardHandComponent>(entity, out var comp))
            return;

        UpdateSprite(entity, comp);
    }
}

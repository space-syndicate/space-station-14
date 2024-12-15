using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CorvaxNext.Cards.Deck;
using Content.Shared._CorvaxNext.Cards.Stack;
using Robust.Client.GameObjects;

namespace Content.Client._CorvaxNext.Cards.Deck;

/// <summary>
/// Handles the visual representation and sprite updates for card decks on the client side,
/// responding to events such as card stack changes, flips, and reordering.
/// </summary>
public sealed class CardDeckSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, int> _notInitialized = new();
    [Dependency] private readonly CardSpriteSystem _cardSpriteSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        UpdatesOutsidePrediction = false;
        SubscribeLocalEvent<CardDeckComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<CardStackInitiatedEvent>(OnStackStart);
        SubscribeNetworkEvent<CardStackQuantityChangeEvent>(OnStackUpdate);
        SubscribeNetworkEvent<CardStackReorderedEvent>(OnReorder);
        SubscribeNetworkEvent<CardStackFlippedEvent>(OnStackFlip);
        SubscribeLocalEvent<CardDeckComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Lazy initialization of card deck sprites
        var entitiesToRemove = new List<EntityUid>();

        foreach (var kvp in _notInitialized)
        {
            var uid = kvp.Key;
            var attempts = kvp.Value;

            if (attempts >= 5)
            {
                // Maximum attempts reached, remove from tracking
                entitiesToRemove.Add(uid);
                continue;
            }

            _notInitialized[uid] = attempts + 1;

            if (!TryComp(uid, out CardStackComponent? stack) || stack.Cards.Count <= 0)
                continue;

            // Check if the card's sprite layer is initialized
            if (!TryGetCardLayer(stack.Cards[^1], out _))
                continue;

            // Update the sprite now that the card is initialized
            if (TryComp(uid, out CardDeckComponent? comp))
            {
                UpdateSprite(uid, comp);
            }

            entitiesToRemove.Add(uid);
        }

        // Remove entities outside the loop to avoid modifying the collection during iteration
        foreach (var uid in entitiesToRemove)
        {
            _notInitialized.Remove(uid);
        }
    }

    private bool TryGetCardLayer(EntityUid card, out SpriteComponent.Layer? layer)
    {
        layer = null;
        if (!TryComp(card, out SpriteComponent? cardSprite))
            return false;

        if (!cardSprite.TryGetLayer(0, out var l))
            return false;

        layer = l;
        return true;
    }

    private void UpdateSprite(EntityUid uid, CardDeckComponent comp)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!TryComp(uid, out CardStackComponent? cardStack))
            return;

        // Prevent errors when the card stack is empty or not initialized
        if (cardStack.Cards.Count <= 0 || !TryGetCardLayer(cardStack.Cards[^1], out _))
        {
            _notInitialized[uid] = 0;
            return;
        }

        _cardSpriteSystem.TryAdjustLayerQuantity((uid, sprite, cardStack), comp.CardLimit);

        _cardSpriteSystem.TryHandleLayerConfiguration(
            (uid, sprite, cardStack),
            comp.CardLimit,
            (sprt, cardIndex, layerIndex) =>
            {
                sprite.LayerSetRotation(layerIndex, Angle.FromDegrees(90));
                sprite.LayerSetOffset(layerIndex, new Vector2(0, comp.YOffset * cardIndex));
                sprite.LayerSetScale(layerIndex, new Vector2(comp.Scale, comp.Scale));
                return true;
            }
        );
    }

    private void OnStackUpdate(CardStackQuantityChangeEvent args)
    {
        var entity = GetEntity(args.Stack);
        if (!TryComp(entity, out CardDeckComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }

    private void OnStackFlip(CardStackFlippedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp(entity, out CardDeckComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }

    private void OnReorder(CardStackReorderedEvent args)
    {
        var entity = GetEntity(args.Stack);
        if (!TryComp(entity, out CardDeckComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }

    private void OnAppearanceChanged(EntityUid uid, CardDeckComponent comp, AppearanceChangeEvent args)
    {
        UpdateSprite(uid, comp);
    }

    private void OnComponentStartupEvent(EntityUid uid, CardDeckComponent comp, ComponentStartup args)
    {
        UpdateSprite(uid, comp);
    }

    private void OnStackStart(CardStackInitiatedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp(entity, out CardDeckComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }
}

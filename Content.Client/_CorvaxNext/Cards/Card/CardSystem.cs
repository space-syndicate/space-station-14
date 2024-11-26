using System.Linq;
using Content.Shared._CorvaxNext.Cards.Card;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._CorvaxNext.Cards.Card;

/// <summary>
/// Handles the initialization and updating of card sprites on the client side,
/// particularly when a card is flipped or when the component starts up.
/// </summary>
public sealed class CardSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<CardFlipUpdatedEvent>(OnFlip);
    }

    private void OnComponentStartupEvent(EntityUid uid, CardComponent comp, ComponentStartup args)
    {
        if (!TryComp(uid, out SpriteComponent? spriteComponent))
            return;

        var layerCount = spriteComponent.AllLayers.Count();
        for (var i = 0; i < layerCount; i++)
        {
            if (!spriteComponent.TryGetLayer(i, out var layer) || layer.State == null || layer.State.Name == null)
                continue;

            var rsi = layer.RSI ?? spriteComponent.BaseRSI;
            if (rsi == null)
                continue;

            comp.FrontSprite.Add(new SpriteSpecifier.Rsi(rsi.Path, layer.State.Name));
        }

        comp.BackSprite ??= comp.FrontSprite;

        // Removed Dirty(uid, comp); as calling Dirty on the client is inappropriate.
        UpdateSprite(uid, comp);
    }

    private void OnFlip(CardFlipUpdatedEvent args)
    {
        var entity = GetEntity(args.Card);
        if (!TryComp(entity, out CardComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }

    private void UpdateSprite(EntityUid uid, CardComponent comp)
    {
        var newSprite = comp.Flipped ? comp.BackSprite : comp.FrontSprite;
        if (newSprite == null)
            return;

        if (!TryComp(uid, out SpriteComponent? spriteComponent))
            return;

        var layerCount = newSprite.Count;
        var spriteLayerCount = spriteComponent.AllLayers.Count();

        // Inserts missing layers
        if (spriteLayerCount < layerCount)
        {
            for (var i = spriteLayerCount; i < layerCount; i++)
            {
                spriteComponent.AddBlankLayer(i);
            }
        }
        // Removes extra layers
        else if (spriteLayerCount > layerCount)
        {
            for (var i = spriteLayerCount - 1; i >= layerCount; i--)
            {
                spriteComponent.RemoveLayer(i);
            }
        }

        for (var i = 0; i < layerCount; i++)
        {
            var layer = newSprite[i];
            spriteComponent.LayerSetSprite(i, layer);
        }
    }
}

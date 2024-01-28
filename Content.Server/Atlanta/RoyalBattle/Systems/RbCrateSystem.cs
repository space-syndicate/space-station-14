using System.Linq;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Atlanta.RoyalBattle.Components;
using Content.Shared.Atlanta.RoyalBattle.Prototypes;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Atlanta.RoyalBattle.Systems;

/// <summary>
/// Handles royal battle crate spawn and fills container with random items.
/// </summary>
public sealed class RbCrateSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;

    private ISawmill _sawmill = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RbCrateRandomComponent, MapInitEvent>(OnMapInit);

        _sawmill = Logger.GetSawmill("Royal Battle");
    }

    private void OnMapInit(EntityUid uid, RbCrateRandomComponent component, MapInitEvent args)
    {
        var crateContent = _prototype.Index(component.Content);
        var itemsPackProtoId = crateContent.Pick(_random);

        _sawmill.Debug($"crate fills with {itemsPackProtoId}.");

        if (_prototype.TryIndex(itemsPackProtoId, out RbCratePackPrototype? pack))
        {
            foreach (var item in pack.Pack.Select(itemProtoId => _entityManager.Spawn(itemProtoId)))
            {
                _entityStorage.Insert(item, uid);
            }
        }
        else
        {
            _sawmill.Error($"crate can't index pack {itemsPackProtoId}!");
        }
    }
}

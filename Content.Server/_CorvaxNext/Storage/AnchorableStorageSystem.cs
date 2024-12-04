using System.Linq;
using Content.Server.Popups;
using Content.Shared.Construction.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Server._CorvaxNext.Storage;

/// <summary>
/// This is used for restricting anchor operations on storage (one bag max per tile)
/// and ejecting living contents on anchor.
/// </summary>
public sealed class AnchorableStorageSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<AnchorableStorageComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<AnchorableStorageComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<AnchorableStorageComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
    }

    private void OnAnchorStateChanged(Entity<AnchorableStorageComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        var transform = Transform(ent);

        if (CheckOverlap(ent, transform))
        {
            _popup.PopupEntity(Loc.GetString("anchored-storage-already-present"), ent);
            _xform.Unanchor(ent, transform);
            return;
        }

        if (!TryComp<StorageComponent>(ent.Owner, out var storage))
            return;

        foreach (var item in storage.StoredItems.Keys.ToArray())
            if (HasComp<MindContainerComponent>(item))
                _container.RemoveEntity(ent, item);
    }

    private void OnAnchorAttempt(Entity<AnchorableStorageComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!CheckOverlap(ent))
            return;

        _popup.PopupEntity(Loc.GetString("anchored-storage-already-present"), ent, args.User);
        args.Cancel();
    }

    private void OnInsertAttempt(Entity<AnchorableStorageComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<MindContainerComponent>(args.EntityUid))
            return;

        if (Transform(ent).Anchored)
            args.Cancel();
    }

    public bool CheckOverlap(EntityUid entity, TransformComponent? transform = null)
    {
        if (!Resolve(entity, ref transform))
            return false;

        if (transform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var indices = _map.TileIndicesFor(grid, gridComp, transform.Coordinates);
        var enumerator = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, indices);

        while (enumerator.MoveNext(out var otherEnt))
        {
            if (otherEnt == entity)
                continue;

            if (HasComp<AnchorableStorageComponent>(otherEnt))
                return true;
        }

        return false;
    }
}

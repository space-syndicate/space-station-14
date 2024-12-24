using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.FromTileCrafter.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class FromTileCrafterComponent : Component
{
    /// <summary>
    /// Object that will be created
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    [ValidatePrototypeId<EntityPrototype>]
    public string EntityToSpawn;

    /// <summary>
    /// Tiles that allowed to use to craft an object
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<string> AllowedTileIds = new();

    /// <summary>
    /// The time it takes to craft.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Delay = 1f;

    /// <summary>
    /// How far spawned item can offset from tile center
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Spread = 0.3f;
}

[Serializable, NetSerializable]
public sealed partial class FromTileCraftDoAfterEvent : DoAfterEvent
{
    public NetEntity Grid;
    public Vector2i GridTile;

    public FromTileCraftDoAfterEvent(NetEntity grid, Vector2i gridTile)
    {
        Grid = grid;
        GridTile = gridTile;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is FromTileCraftDoAfterEvent otherTile
               && Grid == otherTile.Grid
               && GridTile == otherTile.GridTile;
    }
}

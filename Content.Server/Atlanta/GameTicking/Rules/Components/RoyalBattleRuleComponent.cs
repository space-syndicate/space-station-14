using Content.Shared.Atlanta.RoyalBattle.Components;
using Content.Shared.Roles;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Atlanta.GameTicking.Rules.Components;

/// <summary>
/// This is used for royal battle setup.
/// </summary>
[RegisterComponent, Access(typeof(RoyalBattleRuleSystem))]
public sealed partial class RoyalBattleRuleComponent : Component
{
    [DataField("players")]
    public List<EntityUid> Players = new();

    [DataField("playersCount")]
    public int PlayersCount = 0;

    [DataField("zone")]
    public RbZoneComponent? ZoneComponent;
    /// <summary>
    /// The gear all players spawn with.
    /// </summary>
    [DataField("gear", customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Gear = "DeathMatchGear";
}

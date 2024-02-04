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
    [DataField("playersMinds")]
    public List<EntityUid> PlayersMinds = new();

    [DataField("alivePlayers")]
    public List<EntityUid> AlivePlayers = new();

    [DataField("deadPlayers")]
    public List<string> DeadPlayers = new();

    [DataField("availableSpawners")]
    public List<EntityUid> AvailableSpawners = new();

    [DataField("zone")]
    public RbZoneComponent? ZoneComponent;

    [DataField("cratesCount")]
    public int CratesCount;

    [DataField("initializedCrates")]
    public int InitializedCratesCount;
    /// <summary>
    /// The gear all players spawn with.
    /// </summary>
    [DataField("gear", customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Gear = "DeathMatchGear";

    public readonly string RoyalBattlePrototypeId = "RoyalBattle";
}

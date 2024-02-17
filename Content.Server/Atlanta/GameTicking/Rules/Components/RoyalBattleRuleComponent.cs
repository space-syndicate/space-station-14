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
    [DataField("gameState")]
    public RoyalBattleGameState GameState = RoyalBattleGameState.InLobby;

    [DataField("lobbyMapName")]
    public string LobbyMapPath = "Maps/Atlanta/lobby.yml";

    [DataField("lobbyMapId")]
    public EntityUid LobbyMapId;

    [DataField("battleMapId")]
    public EntityUid MapId;

    [DataField("startupTime")]
    public TimeSpan StartupTime = TimeSpan.FromMinutes(1);

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
    public string Gear = "RbFighterGear";

    public readonly string RoyalBattlePrototypeId = "RoyalBattle";
}

public enum RoyalBattleGameState
{
    InLobby,
    InGame,
}

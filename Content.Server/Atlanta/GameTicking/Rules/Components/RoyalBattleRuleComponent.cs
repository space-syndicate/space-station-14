using Content.Shared.Atlanta.RoyalBattle.Components;
using Content.Shared.Roles;
using Robust.Shared.Map;
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
    public MapId? LobbyMapId;

    [DataField("battleMapId")]
    public MapId? MapId;

    [DataField("battleMap")]
    public EntityUid? BattleMap;

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
    /// <summary>
    /// The gear all players spawn with.
    /// </summary>
    [DataField("gear", customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Gear = "RbFighterGear";

    [DataField("restartTime")]
    public TimeSpan RestartTime = TimeSpan.FromSeconds(20);

    public readonly string RoyalBattlePrototypeId = "RoyalBattle";
}

public sealed class RoyalBattleStartEvent : EntityEventArgs
{
}

public enum RoyalBattleGameState
{
    InLobby,
    InGame,
}

using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Humanoid.Systems;
using Content.Server.Mind.Components;
using Content.Server.NPC.Systems;
using Content.Server.Preferences.Managers;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Server.Traitor;
using Content.Shared.Dataset;
using Content.Shared.MobState;
using Content.Shared.MobState.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.AssaultOps;

public sealed class AssaultopsRuleSystem : GameRuleSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly RandomHumanoidSystem _randomHumanoid = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly FactionSystem _faction = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    
    private enum WinType
    {
        /// <summary>
        ///     Operative major win. Goldeneye activated and all ops alive.
        /// </summary>
        OpsMajor,
        /// <summary>
        ///     Minor win. Goldeneye was activated and some ops alive.
        /// </summary>
        OpsMinor,
        /// <summary>
        ///     Hearty. Goldeneye activated but no ops alive.
        /// </summary>
        Hearty,
        /// <summary>
        ///     Stalemate. Goldeneye not activated and ops still alive.
        /// </summary>
        Stalemate,
        /// <summary>
        ///     Crew major win. Goldeneye not activated and no ops alive.
        /// </summary>
        CrewMajor
    }
    
    private enum WinCondition
    {
        IcarusActivated,
        AllOpsDead,
        SomeOpsAlive,
        AllOpsAlive
    }
    
    private WinType _winType = WinType.Stalemate;
    private readonly List<WinCondition> _winConditions = new();
    
    private MapId? _outpostMap;
    
    // TODO: use components, don't just cache entity UIDs
    // There have been (and probably still are) bugs where these refer to deleted entities from old rounds.
    private EntityUid? _outpostGrid;
    private EntityUid? _shuttleGrid;
    private EntityUid? _targetStation;

    public override string Prototype => "Assaultops";
    
    private AssaultopsRuleConfiguration _ruleConfig = new();

    /// <summary>
    ///     Cached operator name prototypes.
    /// </summary>
    private readonly List<string> _operativeNames = new();
    
    /// <summary>
    ///     Players who played as an operative at some point in the round.
    ///     Stores the session as well as the entity name
    /// </summary>
    private readonly Dictionary<string, IPlayerSession> _operativePlayers = new();

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawning);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<AssaultOperativeComponent, ComponentInit>(OnComponentInit);
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        if (!RuleAdded || Configuration is not AssaultopsRuleConfiguration assaultOpsConfig)
            return;

        _ruleConfig = assaultOpsConfig;
        var minPlayers = assaultOpsConfig.MinPlayers;
        if (!ev.Forced && ev.Players.Length < minPlayers)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("assaultops-not-enough-ready-players", ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
            ev.Cancel();
            return;
        }

        if (ev.Players.Length != 0)
            return;

        _chatManager.DispatchServerAnnouncement(Loc.GetString("assaultops-no-one-ready"));
        ev.Cancel();
    }

    private void OnPlayersSpawning(RulePlayerSpawningEvent ev)
    {
        if (!RuleAdded)
            return;
        
        // Basically copied verbatim from traitor code
        var playersPerOperative = _ruleConfig.PlayersPerOperative;
        var maxOperatives = _ruleConfig.MaxOperatives;
        var numOps = MathHelper.Clamp(ev.PlayerPool.Count / playersPerOperative, 1, maxOperatives);

        var opsPool = FindPotentialOperatives(ev);
        var selectedOps = PickOperatives(numOps, opsPool);
        
        SpawnOperatives(selectedOps);
        
        foreach (var session in selectedOps)
        {
            ev.PlayerPool.Remove(session);
            GameTicker.PlayerJoinGame(session);
            var name = session.AttachedEntity == null
                ? string.Empty
                : MetaData(session.AttachedEntity.Value).EntityName;
            // TODO: Fix this being able to have duplicates
            _operativePlayers[name] = session;
        }
    }
    
    private List<IPlayerSession> FindPotentialOperatives(RulePlayerSpawningEvent ev)
    {
        var eligible = new List<IPlayerSession>(ev.PlayerPool).Where(p =>
            ev.Profiles.TryGetValue(p.UserId, out var profile) &&
            profile.AntagPreferences.Contains(_ruleConfig.OperativeRoleProto)
        ).ToList();
        
        if (eligible.Count == 0)
        {
            Logger.InfoS("preset", "Insufficient preferred assaultops, create pool from everyone.");
            return ev.PlayerPool;
        }

        return eligible;
    }

    private List<IPlayerSession> PickOperatives(int count, List<IPlayerSession> pool)
    {
        var selected = new List<IPlayerSession>(count);
        if (pool.Count == 0)
        {
            Logger.InfoS("preset", "Insufficient ready players to fill up with assaultops, stopping the selection");
            return selected;
        }
        
        for (var i = 0; i < count; i++)
        {
            selected.Add(_random.PickAndTake(pool));
            Logger.InfoS("preset", "Selected a assaultop.");
        }
        return selected;
    }

    private void SpawnOperatives(List<IPlayerSession> operatives)
    {
        if (_outpostGrid == null)
            return;
        
        var spawnpoints = GetSpawnpoints(_outpostGrid.Value);
        foreach (var session in operatives)
        {
            var spawnpoint = _random.Pick(spawnpoints);
            SpawnOperative(session, spawnpoint);
        }
    }

    private void SpawnOperative(IPlayerSession session, EntityCoordinates spawnpoint)
    {
        var name = Loc.GetString("nukeops-role-operator") + " " +
                   _random.PickAndTake(_operativeNames);
        var mob = _randomHumanoid.SpawnRandomHumanoid(_ruleConfig.RandomHumanoidSettingsPrototype, spawnpoint, name);
        var profile = _prefs.GetPreferences(session.UserId).SelectedCharacter as HumanoidCharacterProfile;
        
        // EntityManager.EnsureComponent<RandomHumanoidAppearanceComponent>(mob);
        EnsureComp<AssaultOperativeComponent>(mob);

        var gearProto = _prototypeManager.Index<StartingGearPrototype>(_ruleConfig.OperativeStartGearPrototype);
        _stationSpawningSystem.EquipStartingGear(mob, gearProto, profile);

        _faction.RemoveFaction(mob, "NanoTrasen", false);
        _faction.AddFaction(mob, "Syndicate");

        var newMind = new Mind.Mind(session.UserId) { CharacterName = name };
        newMind.ChangeOwningPlayer(session.UserId);
        
        var antagProto = _prototypeManager.Index<AntagPrototype>(_ruleConfig.OperativeRoleProto);
        newMind.AddRole(new TraitorRole(newMind, antagProto));

        newMind.TransferTo(mob);
    }

    private List<EntityCoordinates> GetSpawnpoints(EntityUid outpostUid)
    {
        var spawns = new List<EntityCoordinates>();

        // Forgive sloth for hardcoding prototypes
        foreach (var (_, meta, xform) in EntityManager.EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            if (meta.EntityPrototype?.ID != _ruleConfig.SpawnPointPrototype)
                continue;

            if (xform.ParentUid != _outpostGrid)
                continue;

            spawns.Add(xform.Coordinates);
            break;
        }

        if (spawns.Count == 0)
        {
            spawns.Add(EntityManager.GetComponent<TransformComponent>(outpostUid).Coordinates);
            Logger.WarningS("assaultops", $"Fell back to default spawn for assaultops!");
        }

        return spawns;
    }

    private void OnComponentInit(EntityUid uid, AssaultOperativeComponent component, ComponentInit args)
    {
        // If entity has a prior mind attached, add them to the players list.
        if (!TryComp<MindComponent>(uid, out var mindComponent) || !RuleAdded)
            return;
        
        var session = mindComponent.Mind?.Session;
        var name = MetaData(uid).EntityName;
        if (session != null)
            _operativePlayers.Add(name, session);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        switch (ev.New)
        {
            case GameRunLevel.InRound:
                OnRoundStart();
                break;
            case GameRunLevel.PostRound:
                OnRoundEnd();
                break;
        }
    }

    private void OnRoundStart()
    {
        // TODO: This needs to try and target a Nanotrasen station. At the very least,
        // we can only currently guarantee that NT stations are the only station to
        // exist in the base game.

        _targetStation = _stationSystem.Stations.FirstOrNull();
        if (_targetStation == null)
            return;
        
        var filter = Filter.Empty();
        foreach (var op in EntityQuery<AssaultOperativeComponent>())
        {
            if (!TryComp<ActorComponent>(op.Owner, out var actor))
                continue;

            _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("assaultops-welcome", ("station", _targetStation.Value)));
            filter.AddPlayer(actor.PlayerSession);
        }

        _audioSystem.PlayGlobal(_ruleConfig.GreetSound, filter, recordReplay: false);
    }

    private void OnRoundEnd()
    {
        var total = 0;
        var alive = 0;
        foreach (var (_, state) in EntityQuery<AssaultOperativeComponent, MobStateComponent>())
        {
            total++;
            if (state.CurrentState is DamageState.Alive)
                continue;

            alive++;
            break;
        }

        var allAlive = alive == total;
        if (allAlive)
        {
            _winType = WinType.OpsMinor;
            _winConditions.Add(WinCondition.AllOpsAlive);
        }
        else if (alive == 0)
        {
            _winConditions.Add(WinCondition.AllOpsDead);
        }
        else
        {
            _winConditions.Add(WinCondition.SomeOpsAlive);
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        if (!RuleAdded)
            return;

        ev.AddLine(Loc.GetString($"assaultops-{_winType.ToString().ToLower()}"));

        foreach (var cond in _winConditions)
            ev.AddLine(Loc.GetString($"assaultops-cond-{cond.ToString().ToLower()}"));

        ev.AddLine(Loc.GetString("assaultops-list-start"));
        foreach (var (name, session) in _operativePlayers)
        {
            var listing = Loc.GetString("assaultops-list-name", ("name", name), ("user", session.Name));
            ev.AddLine(listing);
        }
    }

    public override void Started()
    {
        _winType = WinType.Stalemate;
        _winConditions.Clear();
        _outpostGrid = null;

        _operativePlayers.Clear();
        _operativeNames.Clear();
        
        _operativeNames.AddRange(_prototypeManager.Index<DatasetPrototype>(_ruleConfig.OperativeNames).Values);
        
        if (!SpawnMap())
        {
            Logger.InfoS("assaultops", "Failed to load map for assaultops");
            return;
        }

        LoadExistOperatives();
    }

    private bool SpawnMap()
    {
        if (_outpostMap != null)
            return true; // Map is already loaded
        
        var outpostMap = _ruleConfig.OutpostMap;
        if (outpostMap == null)
        {
            Logger.ErrorS("assaultops", "No station map specified for assaultops!");
            return false;
        }
        
        var shuttlePath = _ruleConfig.ShuttleMap;
        if (shuttlePath == null)
        {
            Logger.ErrorS("assaultops", "No shuttle map specified for assaultops!");
            return false;
        }
        
        var mapId = _mapManager.CreateMap();
        var options = new MapLoadOptions() { LoadMap = true };
        
        if (!_mapLoader.TryLoad(mapId, outpostMap.ToString(), out var outpostGrids, options) || outpostGrids.Count == 0)
        {
            Logger.ErrorS("assaultops", $"Error loading map {outpostMap} for assaultops!");
            return false;
        }
        
        // Assume the first grid is the outpost grid.
        _outpostGrid = outpostGrids[0];
        
        // Listen I just don't want it to overlap.
        if (!_mapLoader.TryLoad(mapId, shuttlePath.ToString(), out var grids, new MapLoadOptions {Offset = Vector2.One*1000f}) || !grids.Any())
        {
            Logger.ErrorS("assaultops", $"Error loading grid {shuttlePath} for assaultops!");
            return false;
        }
        
        var shuttleId = grids.First();

        // Naughty, someone saved the shuttle as a map.
        if (Deleted(shuttleId))
        {
            Logger.ErrorS("assaultops", $"Tried to load assaultops shuttle as a map, aborting.");
            _mapManager.DeleteMap(mapId);
            return false;
        }
        
        if (TryComp<ShuttleComponent>(shuttleId, out var shuttle))
        {
            _shuttleSystem.TryFTLDock(shuttle, _outpostGrid.Value);
        }

        _outpostMap = mapId;
        _shuttleGrid = shuttleId;

        return true;
    }

    private void LoadExistOperatives()
    {
        // Add pre-existing nuke operatives to the credit list.
        var query = EntityQuery<AssaultOperativeComponent, MindComponent>(true);
        foreach (var (_, mindComp) in query)
        {
            if (mindComp.Mind == null || !mindComp.Mind.TryGetSession(out var session))
                continue;
            var name = MetaData(mindComp.Owner).EntityName;
            _operativePlayers.Add(name, session);
        }
    }

    public override void Ended() { }
}

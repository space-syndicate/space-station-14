using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Content.Server.Administration.Commands;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.RandomMetadata;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Dataset;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mage;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Tag;
using Content.Shared.Zombies;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Objectives;
using Content.Shared.Objectives.Components;

namespace Content.Server.GameTicking.Rules;

public sealed class MageRuleSystem : GameRuleSystem<MageRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergency = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    [Dependency] private readonly MindSystem _mindSystem = default!;

    [ValidatePrototypeId<TagPrototype>]
    private const string MageSpellbookTagPrototype = "BaseMageSpellbook";

    [ValidatePrototypeId<AntagPrototype>]
    public const string MageId = "Mage";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawning);
        SubscribeLocalEvent<MageComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<MageComponent, GhostRoleSpawnerUsedEvent>(OnPlayersGhostSpawning);
        SubscribeLocalEvent<MageComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<MageComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MageComponent, ComponentRemove>(OnComponentRemove);
    }

    public void MakeMage(EntityUid mindId, MindComponent mind)
    {
        if (!mind.OwnedEntity.HasValue)
            return;

        //ok hardcoded value bad but so is everything else here
        _roles.MindAddRole(mindId, new MageRoleComponent { PrototypeId = "Mage" }, mind);
        if (mind.CurrentEntity != null)
        {
            foreach (var (mages, gameRule) in EntityQuery<MageRuleComponent, GameRuleComponent>())
            {
                mages.MagePlayers.Add(mind.CharacterName!, mind.CurrentEntity.GetValueOrDefault());
            }
        }

        SetOutfitCommand.SetOutfit(mind.OwnedEntity.Value, "WizardBlueGear", EntityManager);
    }
    public bool TryGetRuleFromMage(EntityUid opUid, [NotNullWhen(true)] out (MageRuleComponent, GameRuleComponent)? comps)
    {
        comps = null;
        var query = EntityQueryEnumerator<MageRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleEnt, out var mages, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(ruleEnt, gameRule))
                continue;

            if (_mind.TryGetMind(opUid, out var mind, out _))
            {
                var found = mages.MagePlayers.Values.Any(v => v == mind);
                if (found)
                {
                    comps = (mages, gameRule);
                    return true;
                }
            }
        }

        return false;
    }

    private void OnComponentInit(EntityUid uid, MageComponent component, ComponentInit args)
    {
        var query = EntityQueryEnumerator<MageRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleEnt, out var mages, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(ruleEnt, gameRule))
                continue;

            // If entity has a prior mind attached, add them to the players list.
            if (!_mind.TryGetMind(uid, out var mind, out _))
                continue;

            var name = MetaData(uid).EntityName;
            mages.MagePlayers.Add(name, mind);
            RemComp<PacifiedComponent>(uid); // Corvax-DionaPacifist: Allow dionas mages to harm
        }
    }

    private void OnComponentRemove(EntityUid uid, MageComponent component, ComponentRemove args)
    {
        CheckRoundShouldEnd();
    }



    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        var query = EntityQueryEnumerator<MageRuleComponent>();
        while (query.MoveNext(out var uid, out var mages))
        {
            switch (ev.New)
            {
                case GameRunLevel.InRound:
                    OnRoundStart(uid, mages);
                    break;
                case GameRunLevel.PostRound:
                    OnRoundEnd(uid, mages);
                    break;
            }
        }
    }

    /// <summary>
    /// Loneops can only spawn if there is no mages active
    /// </summary>
    public bool CheckLoneOpsSpawn()
    {
        return !EntityQuery<MageRuleComponent>().Any();
    }

    private void OnRoundStart(EntityUid uid, MageRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var filter = Filter.Empty();
        var query = EntityQueryEnumerator<MageComponent, ActorComponent>();
        while (query.MoveNext(out _, out var mage, out var actor))
        {
            NotifyMage(actor.PlayerSession, mage, component);
            filter.AddPlayer(actor.PlayerSession);
        }
    }

    private void OnRoundEnd(EntityUid uid, MageRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // If the win condition was set to operative/crew major win, ignore.
        if (component.Win == WinType.MageWin || component.Win == WinType.CrewWin)
            return;


        var allAlive = true;
        var anyAlive = false;
        var mindQuery = GetEntityQuery<MindComponent>();
        var mobStateQuery = GetEntityQuery<MobStateComponent>();
        foreach (var (_, mindId) in component.MagePlayers)
        {
            // mind got deleted somehow so ignore it
            if (!mindQuery.TryGetComponent(mindId, out var mind))
                continue;

            // check if player got gibbed or ghosted or something - count as dead
            if (mind.OwnedEntity != null &&
                // if the player somehow isn't a mob anymore that also counts as dead
                mobStateQuery.TryGetComponent(mind.OwnedEntity.Value, out var mobState) &&
                // have to be alive, not crit or dead
                mobState.CurrentState is MobState.Alive)
            {
                anyAlive = true;
                continue;

            }

            allAlive = false;
            break;
        }
        if (allAlive)
        {
            SetWinType(uid, WinType.MageWin, component);
            component.WinConditions.Add(WinCondition.MagesAlive);
            return;
        }
        else if (anyAlive)
        {
            SetWinType(uid, WinType.MageWin, component);
            component.WinConditions.Add(WinCondition.SomeMagesAlive);
            return;
        }
        else
        {
            SetWinType(uid, WinType.CrewWin, component);
            component.WinConditions.Add(WinCondition.NoMagesAlive);
            return;
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var mindQuery = GetEntityQuery<MindComponent>();
        foreach (var mages in EntityQuery<MageRuleComponent>())
        {
            var winText = Loc.GetString($"mages-{mages.Win.ToString().ToLower()}");

            ev.AddLine(winText);

            foreach (var cond in mages.WinConditions)
            {
                var text = Loc.GetString($"mages-cond-{cond.ToString().ToLower()}");

                ev.AddLine(text);
            }

            ev.AddLine(Loc.GetString("mages-list-start"));
            foreach (var (name, mindId) in mages.MagePlayers)
            {
                if (mindQuery.TryGetComponent(mindId, out var mind) && mind.Session != null)
                {
                    ev.AddLine(Loc.GetString("mages-list-name-user", ("name", name), ("user", mind.Session.Name)));
                }
                else
                {
                    ev.AddLine(Loc.GetString("mages-list-name", ("name", name)));
                }
            }
        }
    }

    private void SetWinType(EntityUid uid, WinType type, MageRuleComponent? component = null, bool endRound = true)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Win = type;

        if (endRound && (type == WinType.CrewWin || type == WinType.MageWin))
            _roundEndSystem.EndRound();
    }

    private void CheckRoundShouldEnd()
    {
        var query = EntityQueryEnumerator<MageRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var mages, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (mages.RoundEndBehavior == RoundEndBehavior.Nothing || mages.Win == WinType.CrewWin || mages.Win == WinType.MageWin)
                continue;

            var mindQuery = GetEntityQuery<MindComponent>();
            var mobStateQuery = GetEntityQuery<MobStateComponent>();
            var allMages = EntityQuery<MageComponent, MobStateComponent, TransformComponent>(true);
            var aliveMages = allMages.Where(ent => ent.Item2.CurrentState == MobState.Alive && ent.Item1.Running);
            if (aliveMages.Count() > 0)
            {
                mages.WinConditions.Add(WinCondition.NoMagesAlive);

                SetWinType(uid, WinType.CrewWin, mages, false);
                _roundEndSystem.DoRoundEndBehavior(
                mages.RoundEndBehavior, mages.EvacShuttleTime, mages.RoundEndTextSender, mages.RoundEndTextShuttleCall, mages.RoundEndTextAnnouncement);

                // prevent it called multiple times
                mages.RoundEndBehavior = RoundEndBehavior.Nothing;
                continue;
            }
            bool completeObjectives = true;
            foreach (var mage in aliveMages)
            {

            }
            if (completeObjectives)
            {
                mages.WinConditions.Add((allMages == aliveMages) ? WinCondition.MagesAlive : WinCondition.SomeMagesAlive);

                SetWinType(uid, WinType.MageWin, mages, false);
                _roundEndSystem.DoRoundEndBehavior(
                mages.RoundEndBehavior, mages.EvacShuttleTime, mages.RoundEndTextSender, mages.RoundEndTextShuttleCall, mages.RoundEndTextAnnouncement);

                // prevent it called multiple times
                mages.RoundEndBehavior = RoundEndBehavior.Nothing;
                continue;
            }

        }
    }

    private void OnMobStateChanged(EntityUid uid, MageComponent component, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Dead)
            CheckRoundShouldEnd();
    }

    private void OnPlayersSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<MageRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var mages, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (!SpawnMap(uid, mages))
            {
                Logger.InfoS("nukies", "Failed to load map for mages");
                continue;
            }

            // Basically copied verbatim from traitor code
            var playersPerMage = mages.PlayersPerMage;
            var maxMage = mages.MaxMage;

            // Dear lord what is happening HERE.
            var everyone = new List<ICommonSession>(ev.PlayerPool);
            var prefList = new List<ICommonSession>();
            var magesList = new List<ICommonSession>();

            // The LINQ expression ReSharper keeps suggesting is completely unintelligible so I'm disabling it
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var player in everyone)
            {
                if (!ev.Profiles.ContainsKey(player.UserId))
                {
                    continue;
                }

                var profile = ev.Profiles[player.UserId];
                if (profile.AntagPreferences.Contains(mages.MageRoleProto.Id))
                {
                    prefList.Add(player);
                }
            }

            var numMages = MathHelper.Clamp(_playerManager.PlayerCount / playersPerMage, 1, maxMage);

            for (var i = 0; i < numMages; i++)
            {
                // TODO: Please fix this if you touch it.
                ICommonSession mage;
                // Only one commander, so we do it at the start
                if (i == 0)
                {
                    if (prefList.Count == 0)
                    {
                        if (everyone.Count == 0)
                        {
                            Logger.InfoS("preset", "Insufficient ready players to fill up with mages, stopping the selection");
                            break;
                        }
                        mage = _random.PickAndTake(everyone);
                        Logger.InfoS("preset", "Insufficient preferred mage picking at random.");
                    }
                    else
                    {
                        mage = _random.PickAndTake(prefList);
                        everyone.Remove(mage);
                        Logger.InfoS("preset", "Insufficient preferred mage commander or agents, picking at random from regular op list.");
                    }
                }
                else if (i == 1)
                {
                    if (prefList.Count == 0)
                    {
                        if (everyone.Count == 0)
                        {
                            Logger.InfoS("preset", "Insufficient ready players to fill up with mages, stopping the selection");
                            break;
                        }
                        mage = _random.PickAndTake(everyone);
                        Logger.InfoS("preset", "Insufficient preferred mage, agents or nukies, picking at random.");
                    }
                    else
                    {
                        mage = _random.PickAndTake(prefList);
                        everyone.Remove(mage);
                        Logger.InfoS("preset", "Insufficient preferred mage, picking at random from regular op list.");
                    }


                }
                else
                {
                    mage = _random.PickAndTake(prefList);
                    everyone.Remove(mage);
                    Logger.InfoS("preset", "Selected a preferred mage.");
                }

                magesList.Add(mage);
            }

            SpawnMages(numMages, magesList, false, mages);

            foreach (var session in magesList)
            {
                ev.PlayerPool.Remove(session);
                GameTicker.PlayerJoinGame(session);

                if (!_mind.TryGetMind(session, out var mind, out _))
                    continue;

                var name = session.AttachedEntity == null
                    ? string.Empty
                    : Name(session.AttachedEntity.Value);
                mages.MagePlayers[name] = mind;
            }
        }
    }

    private void OnPlayersGhostSpawning(EntityUid uid, MageComponent component, GhostRoleSpawnerUsedEvent args)
    {
        var spawner = args.Spawner;

        if (!TryComp<MageSpawnerComponent>(spawner, out var mageSpawner))
            return;

        HumanoidCharacterProfile? profile = null;
        if (TryComp(args.Spawned, out ActorComponent? actor))
            profile = _prefs.GetPreferences(actor.PlayerSession.UserId).SelectedCharacter as HumanoidCharacterProfile;

        // todo: this is kinda awful for multi-nukies
        foreach (var mages in EntityQuery<MageRuleComponent>())
        {
            if (mageSpawner.MageName == null
                || mageSpawner.MageStartingGear == null
                || mageSpawner.MageRolePrototype == null)
            {
                // I have no idea what is going on with mage ops code, but I'm pretty sure this shouldn't be possible.
                Log.Error($"Invalid mage spawner: {ToPrettyString(spawner)}");
                continue;
            }

            SetupMageEntity(uid, mageSpawner.MageName, mageSpawner.MageStartingGear, profile, mages);

            mages.MageMindPendingData.Add(uid, mageSpawner.MageRolePrototype);
        }
    }

    private void OnMindAdded(EntityUid uid, MageComponent component, MindAddedMessage args)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        foreach (var (mages, gameRule) in EntityQuery<MageRuleComponent, GameRuleComponent>())
        {
            if (mages.MageMindPendingData.TryGetValue(uid, out var role) || mages.RoundEndBehavior == RoundEndBehavior.Nothing)
            {
                role ??= mages.MageRoleProto;
                _roles.MindAddRole(mindId, new MageRoleComponent { PrototypeId = role });
                mages.MageMindPendingData.Remove(uid);
            }

            if (mind.Session is not { } playerSession)
                return;

            if (mages.MagePlayers.ContainsValue(mindId))
                return;

            mages.MagePlayers.Add(Name(uid), mindId);

            if (GameTicker.RunLevel != GameRunLevel.InRound)
                return;
            NotifyMage(playerSession, component, mages);
        }
    }

    private bool SpawnMap(EntityUid uid, MageRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var path = component.ShuttleMap;
        var shuttlePath = component.ShuttleMap;

        var mapId = _mapManager.CreateMap();
        var options = new MapLoadOptions
        {
            LoadMap = true,
        };

        if (!_map.TryLoad(mapId, path.ToString(), out var outpostGrids, options) || outpostGrids.Count == 0)
        {
            Logger.ErrorS("mages", $"Error loading map {path} for nukies!");
            return false;
        }

        // Assume the first grid is the outpost grid.
        component.Shuttle = outpostGrids[0];

        // Listen I just don't want it to overlap.
        if (!_map.TryLoad(mapId, shuttlePath.ToString(), out var grids, new MapLoadOptions { Offset = Vector2.One * 1000f }) || !grids.Any())
        {
            Logger.ErrorS("magies", $"Error loading grid {shuttlePath} for nukies!");
            return false;
        }

        var shuttleId = grids.First();

        // Naughty, someone saved the shuttle as a map.
        if (Deleted(shuttleId))
        {
            Logger.ErrorS("mages", $"Tried to load mages shuttle as a map, aborting.");
            _mapManager.DeleteMap(mapId);
            return false;
        }

        if (TryComp<ShuttleComponent>(shuttleId, out var shuttle))
        {
            _shuttle.TryFTLDock(shuttleId, shuttle, component.Shuttle);
        }

        AddComp<MageMapComponent>(shuttleId);

        component.Shuttle = shuttleId;
        return true;
    }

    private (string Name, string Role, string Gear) GetMageSpawnDetails(MageRuleComponent component)
    {
        string name = _random.PickAndTake(component.MageNames[component.FirstNames]) + " " + _random.PickAndTake(component.MageNames[component.LastNames]);
        string role = component.MageRoleProto;
        string gear = component.MageStartGearProto;

        return (name, role, gear);
    }

    /// <summary>
    ///     Adds missing mage operative components, equips starting gear and renames the entity.
    /// </summary>
    private void SetupMageEntity(EntityUid mob, string name, string gear, HumanoidCharacterProfile? profile, MageRuleComponent component)
    {
        _metaData.SetEntityName(mob, name);
        EnsureComp<MageComponent>(mob);

        if (profile != null)
        {
            _humanoid.LoadProfile(mob, profile);
        }

        if (component.StartingGearPrototypes.TryGetValue(gear, out var gearPrototype))
            _stationSpawning.EquipStartingGear(mob, gearPrototype, profile);

        _npcFaction.RemoveFaction(mob, "NanoTrasen", false);
        _npcFaction.AddFaction(mob, "Mages");
    }

    private void SpawnMages(int spawnCount, List<ICommonSession> sessions, bool addSpawnPoints, MageRuleComponent component)
    {

        var shuttleUid = component.Shuttle;
        var spawns = new List<EntityCoordinates>();

        // Forgive me for hardcoding prototypes
        foreach (var (_, meta, xform) in EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            if (meta.EntityPrototype?.ID != component.SpawnPointProto.Id)
                continue;

            if (xform.ParentUid != component.Shuttle)
                continue;

            spawns.Add(xform.Coordinates);
            break;
        }

        if (spawns.Count == 0 && shuttleUid != null)
        {
            spawns.Add(Transform(shuttleUid).Coordinates);
            Logger.WarningS("mages", $"Fell back to default spawn for mages!");
        }

        for (var i = 0; i < spawnCount; i++)
        {
            var spawnDetails = GetMageSpawnDetails(component);
            var magesAntag = _prototypeManager.Index<AntagPrototype>(spawnDetails.Role);

            if (sessions.TryGetValue(i, out var session))
            {
                var profile = _prefs.GetPreferences(session.UserId).SelectedCharacter as HumanoidCharacterProfile;
                if (!_prototypeManager.TryIndex(profile?.Species ?? SharedHumanoidAppearanceSystem.DefaultSpecies, out SpeciesPrototype? species))
                {
                    species = _prototypeManager.Index<SpeciesPrototype>(SharedHumanoidAppearanceSystem.DefaultSpecies);
                }

                var mob = Spawn(species.Prototype, _random.Pick(spawns));
                SetupMageEntity(mob, spawnDetails.Name, spawnDetails.Gear, profile, component);
                var newMind = _mind.CreateMind(session.UserId, spawnDetails.Name);
                _mind.SetUserId(newMind, session.UserId);
                _roles.MindAddRole(newMind, new MageRoleComponent { PrototypeId = spawnDetails.Role });

                _mind.TransferTo(newMind, mob);
            }
            else if (addSpawnPoints)
            {
                var spawnPoint = Spawn(component.GhostSpawnPointProto, _random.Pick(spawns));
                var ghostRole = EnsureComp<GhostRoleComponent>(spawnPoint);
                EnsureComp<GhostRoleMobSpawnerComponent>(spawnPoint);
                ghostRole.RoleName = Loc.GetString(magesAntag.Name);
                ghostRole.RoleDescription = Loc.GetString(magesAntag.Objective);

                var mageSpawner = EnsureComp<MageSpawnerComponent>(spawnPoint);
                mageSpawner.MageName = spawnDetails.Name;
                mageSpawner.MageRolePrototype = spawnDetails.Role;
                mageSpawner.MageStartingGear = spawnDetails.Gear;
            }
        }
    }

    /// <summary>
    /// Display a greeting message and play a sound for a nukie
    /// </summary>
    private void NotifyMage(ICommonSession session, MageComponent mage, MageRuleComponent mageRule)
    {

        _chatManager.DispatchServerMessage(session, Loc.GetString("mage-welcome"));
        _audio.PlayGlobal(mage.GreetSoundNotification, session);
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<MageRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var mages, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var minPlayers = mages.MinPlayers;
            if (!ev.Forced && ev.Players.Length < minPlayers)
            {
                _chatManager.SendAdminAnnouncement(Loc.GetString("mages-not-enough-ready-players",
                    ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
                ev.Cancel();
                continue;
            }

            if (ev.Players.Length != 0)
                continue;

            _chatManager.DispatchServerAnnouncement(Loc.GetString("mages-no-one-ready"));
            ev.Cancel();
        }
    }


    protected override void Started(EntityUid uid, MageRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        // TODO: Loot table or something
        component.StartingGearPrototypes.Add(component.MageStartGearProto, _prototypeManager.Index<StartingGearPrototype>(component.MageStartGearProto));


        foreach (var proto in new[] { component.FirstNames, component.LastNames })
        {
            component.MageNames.Add(proto, new List<string>(_prototypeManager.Index<DatasetPrototype>(proto).Values));
        }

        var query = EntityQuery<MageComponent, MindContainerComponent, MetaDataComponent>(true);
        foreach (var (_, mindComp, metaData) in query)
        {
            if (!mindComp.HasMind)
                continue;

            component.MagePlayers.Add(metaData.EntityName, mindComp.Mind.Value);
        }

    }
}

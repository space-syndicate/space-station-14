using Content.Server.Administration.Commands;
using Content.Server.Atlanta.GameTicking.Rules.Components;
using Content.Server.Atlanta.Roles;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Atlanta.RoyalBattle.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Atlanta.GameTicking.Rules;

public sealed class RoyalBattleRuleSystem : GameRuleSystem<RoyalBattleRuleComponent>
{
    /// <inheritdoc/>
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly StationJobsSystem _jobsSystem = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RbZoneComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReport);

        SubscribeLocalEvent<RoyalBattleRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
        SubscribeLocalEvent<RoyalBattleRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);

        _sawmill = Logger.GetSawmill("Royal Battle Rule");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RoyalBattleRuleComponent>();
        while (query.MoveNext(out _, out var rb))
        {
            if (rb.GameState == RoyalBattleGameState.InLobby)
            {
                var time = rb.StartupTime - TimeSpan.FromSeconds(frameTime);

                if (time <= TimeSpan.Zero)
                {
                    _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-lobby-end"));
                    _mapManager.SetMapPaused(rb.MapId!.Value, false);

                    if (TryGetRandomStation(out var chosenStation, HasComp<StationJobsComponent>))
                    {
                        var jobs = _jobsSystem.GetJobs(chosenStation.Value).Keys;

                        foreach (var job in jobs)
                        {
                            _jobsSystem.MakeUnavailableJob(chosenStation.Value, job);
                        }
                    }

                    foreach (var mob in rb.AlivePlayers)
                    {

                        RemComp<GodmodeComponent>(mob);
                        RemComp<PacifiedComponent>(mob);

                        if (rb.AvailableSpawners.Count > 0)
                        {
                            var spawner = _random.Pick(rb.AvailableSpawners);
                            var spawnerPosition = _transform.GetMoverCoordinates(spawner);
                            _transform.SetCoordinates(mob, spawnerPosition);
                            _transform.AttachToGridOrMap(mob);

                            rb.AvailableSpawners.Remove(spawner);
                        }
                        else
                        {
                            _sawmill.Error("No spawners! Player will be spawned on default position, but it doesn't well!");
                        }
                    }

                    RaiseLocalEvent(new RoyalBattleStartEvent());
                    rb.GameState = RoyalBattleGameState.InGame;

                    _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-start-battle-player-count", ("count", rb.AlivePlayers.Count)), Color.Cyan);
                }
                else
                {
                    if (time < TimeSpan.FromSeconds(10))
                    {
                        if ((int) time.TotalSeconds < (int) rb.StartupTime.TotalSeconds)
                        {
                            _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-lobby-wait-time-remain", ("seconds", (int) time.TotalSeconds + 1)));
                        }
                    }

                    rb.StartupTime = time;
                }
            }
        }
    }

    private void OnMapInit(EntityUid uid, RbZoneComponent component, MapInitEvent args)
    {
        _sawmill.Debug("Start process map with zone stratup.");
        var query = EntityQueryEnumerator<RoyalBattleRuleComponent>();
        while (query.MoveNext(out _, out var rb))
        {
            rb.MapId = _transform.GetMapCoordinates(uid).MapId;

            // load lobby
            var lobbyMapOptions = new MapLoadOptions()
            {
            };

            rb.LobbyMapId = _mapManager.CreateMap();

            if (!_mapLoaderSystem.TryLoad(rb.LobbyMapId.Value, rb.LobbyMapPath, out _, lobbyMapOptions))
            {
                _sawmill.Error("Couldn't load lobby map.");
                _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-lobby-cant-spawn"), Color.Red);

                var roundEnd = EntityManager.EntitySysManager.GetEntitySystem<RoundEndSystem>();
                roundEnd.EndRound(TimeSpan.FromSeconds(10));

                continue;
            }

            if (!_mapManager.IsMapInitialized(rb.LobbyMapId.Value))
            {
                _mapManager.DoMapInitialize(rb.LobbyMapId.Value);
            }
            _mapManager.SetMapPaused(rb.MapId.Value, true);

            _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-lobby-remain", ("seconds", (int) rb.StartupTime.TotalSeconds)));
        }
    }

    public static void AddSpawner(RoyalBattleRuleComponent rule, EntityUid spawner)
    {
        rule.AvailableSpawners.Add(spawner);
    }


    private void OnKillReport(ref KillReportedEvent ev)
    {
        var query = EntityQueryEnumerator<RoyalBattleRuleComponent>();
        while (query.MoveNext(out var uid, out var rb))
        {
            var player = ev.Entity;

            if (rb.AlivePlayers.Remove(player))
            {
                _damageable.TryChangeDamage(player,
                    new DamageSpecifier(new DamageSpecifier(
                        _prototypeManager.Index<DamageGroupPrototype>("Airloss"),
                        FixedPoint2.New(200))));

                var deadPlayerName = TryComp<MetaDataComponent>(player, out var meta) ? meta.EntityName : player.ToString();
                rb.DeadPlayers.Add(deadPlayerName);
            }
            else
            {
                _sawmill.Error($"Can't remove entity {player}! It can throws errors,");
            }

            if (rb.AlivePlayers.Count <= 1)
            {
                if (rb.AlivePlayers.Count > 0)
                {
                    var winner = rb.AlivePlayers[0];
                    AddComp<GodmodeComponent>(winner);
                    var winnerName = TryComp<MetaDataComponent>(winner, out var meta)
                        ? meta.EntityName
                        : winner.ToString();
                    _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-winner", ("winner", winnerName)), Color.Aqua);
                }
                else
                {
                    _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-draw"), Color.Coral);
                }

                _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-ending-announce"), Color.Aquamarine);

                var roundEnd = EntityManager.EntitySysManager.GetEntitySystem<RoundEndSystem>();
                roundEnd.EndRound(rb.RestartTime);
            }
            else
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("rb-death-announce",
                        ("count", rb.AlivePlayers.Count)), Color.Red);
            }
        }
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<RoyalBattleRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rb, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            var newMind = _mind.CreateMind(ev.Player.UserId, ev.Profile.Name);
            _mind.SetUserId(newMind, ev.Player.UserId);

            var tryMob = _stationSpawning.SpawnPlayerCharacterOnStation(ev.Station, null, ev.Profile);
            DebugTools.AssertNotNull(tryMob);
            var mob = tryMob!.Value;

            _mind.TransferTo(newMind, mob);
            SetOutfitCommand.SetOutfit(mob, rb.Gear, EntityManager);
            EnsureComp<KillTrackerComponent>(mob);

            if (!_mind.TryGetMind(mob, out var mindId, out var mind))
            {
                _sawmill.Info("Failed getting mind for picked rb player.");
            }
            else
            {

                var rbPlayerRole = new RbPlayerRoleComponent
                {
                    PrototypeId = rb.RoyalBattlePrototypeId
                };

                _roleSystem.MindAddRole(mindId, rbPlayerRole, mind);

                _chatManager.DispatchServerMessage(ev.Player, Loc.GetString("rb-rules"));

                _sawmill.Info($"Added new player {mind.CharacterName}/{mind}");
            }

            EnsureComp<GodmodeComponent>(mob);
            EnsureComp<PacifiedComponent>(mob);

            rb.AlivePlayers.Add(mob);
            rb.PlayersMinds.Add(mindId);

            ev.Handled = true;
            break;
        }
    }

    private void OnObjectivesTextGetInfo(EntityUid uid, RoyalBattleRuleComponent component, ref ObjectivesTextGetInfoEvent args)
    {
        args.Minds = component.PlayersMinds;
        args.AgentName = Loc.GetString("rb-agent-name");
    }

    private void OnObjectivesTextPrepend(EntityUid uid, RoyalBattleRuleComponent component, ref ObjectivesTextPrependEvent args)
    {
        args.Text += "\n";
        var place = 1;

        if (component.AlivePlayers.Count == 0)
        {
            args.Text += Loc.GetString("rb-results-everyone-dead");
        }
        else
        {
            component.DeadPlayers.Add(EnsureComp<MetaDataComponent>(component.AlivePlayers[0]).EntityName);
        }

        args.Text += "\n";

        while (component.DeadPlayers.Count > 0)
        {
            var player = component.DeadPlayers[^1];
            args.Text += Loc.GetString("rb-results-place",
                ("place", place++), ("player", player)) + "\n";
            component.DeadPlayers.Remove(player);
        }

        args.Text += " - попуск.";
    }
}

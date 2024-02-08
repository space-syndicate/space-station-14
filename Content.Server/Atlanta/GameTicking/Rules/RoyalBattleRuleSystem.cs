using System.Linq;
using Content.Server.Administration.Commands;
using Content.Server.Atlanta.GameTicking.Rules.Components;
using Content.Server.Atlanta.Roles;
using Content.Server.Atlanta.RoyalBattle.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Player;
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
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReport);

        SubscribeLocalEvent<RoyalBattleRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
        SubscribeLocalEvent<RoyalBattleRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);

        _sawmill = Logger.GetSawmill("Royal Battle Rule");
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
                    _chatManager.DispatchServerAnnouncement($"И у нас есть победитель! Это {winnerName}!", Color.Aqua);
                }
                else
                {
                    _chatManager.DispatchServerAnnouncement("Опс! Видимо все погибли. Чтож, мы и не надеялись!", Color.Coral);
                }

                _chatManager.DispatchServerAnnouncement("Всем спасибо за участие! Поздравим победителей!", Color.Aquamarine);

                var roundEnd = EntityManager.EntitySysManager.GetEntitySystem<RoundEndSystem>();
                roundEnd.EndRound();
            }
            else
            {
                _chatManager.DispatchServerAnnouncement($"Осталось {rb.AlivePlayers.Count} претендентов на приз!",
                    Color.Red);
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

            rb.AlivePlayers.Add(mob);
            rb.PlayersMinds.Add(mindId);

            if (rb.AvailableSpawners.Count > 0)
            {
                var spawner = _random.Pick<EntityUid>(rb.AvailableSpawners);
                var spawnerPosition = _transform.GetWorldPosition(spawner);
                _transform.SetWorldPosition(mob, spawnerPosition);
                _transform.AttachToGridOrMap(mob);

                rb.AvailableSpawners.Remove(spawner);
            }
            else
            {
                _sawmill.Error("No spawners! Player will be spawned on default position, but it doesn't well!");
            }

            ev.Handled = true;
            break;
        }
    }

    private void OnObjectivesTextGetInfo(EntityUid uid, RoyalBattleRuleComponent component, ref ObjectivesTextGetInfoEvent args)
    {
        args.Minds = component.PlayersMinds;
        args.AgentName = "пубгер";
    }

    private void OnObjectivesTextPrepend(EntityUid uid, RoyalBattleRuleComponent component, ref ObjectivesTextPrependEvent args)
    {
        args.Text += "\n";
        var place = 1;

        if (component.AlivePlayers.Count == 0)
        {
            args.Text += "Фактически все погибли";
        }

        while (component.DeadPlayers.Count > 0)
        {
            var player = component.DeadPlayers[^1];
            args.Text += $";\n{place++} место- {player}";
            component.DeadPlayers.Remove(player);
        }

        args.Text += " - попуск.";
    }
}

using System.Linq;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Rules;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Traits.Assorted;
using Content.Server._CorvaxNext.Traits.Assorted;
using Content.Server.Points;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server._CorvaxNext.BattleRoyale.Rules.Components;
using Content.Server._CorvaxNext.Ghostbar.Components;
using Robust.Shared.Audio;
using Content.Shared.Bed.Sleep;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Chat;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Players;
using Content.Shared.Points;
using Content.Shared.Traits.Assorted;
using Content.Shared._CorvaxNext.Skills;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Enums;

namespace Content.Server._CorvaxNext.BattleRoyale.Rules
{
    /// <summary>
    /// Battle Royale game mode where the last player standing wins, with built-in checks to prevent late joining.
    /// </summary>
    public sealed class BattleRoyaleRuleSystem : GameRuleSystem<BattleRoyaleRuleComponent>
    {
        [Dependency] private readonly IPlayerManager _player = default!;
        [Dependency] private readonly MindSystem _mind = default!;
        [Dependency] private readonly PointSystem _point = default!;
        [Dependency] private readonly RoundEndSystem _roundEnd = default!;
        [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
        [Dependency] private readonly TransformSystem _transforms = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly KillTrackingSystem _killTracking = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedSkillsSystem _skills = default!;
        [Dependency] private readonly ArrivalsSystem _arrivals = default!;

        private const int MaxNormalCallouts = 60;
        private const int MaxEnvironmentalCallouts = 10;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
            SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
            SubscribeLocalEvent<RefreshLateJoinAllowedEvent>(OnRefreshLateJoinAllowed);
            SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new[] { typeof(ArrivalsSystem) });
            SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        }

        private void OnRefreshLateJoinAllowed(RefreshLateJoinAllowedEvent ev)
        {
            if (CheckBattleRoyaleActive())
            {
                ev.Disallow();
            }
        }

        private void OnPlayerSpawning(PlayerSpawningEvent ev)
        {
            if (CheckBattleRoyaleActive() && ev.SpawnResult == null)
            {
                if (HasComp<StationArrivalsComponent>(ev.Station))
                {
                    ev.SpawnResult = null;
                }
            }
        }

        private bool CheckBattleRoyaleActive()
        {
            var query = EntityQueryEnumerator<BattleRoyaleRuleComponent, ActiveGameRuleComponent>();
            return query.MoveNext(out _, out _, out _);
        }

        protected override void Started(EntityUid uid, BattleRoyaleRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
        {
            base.Started(uid, component, gameRule, args);
            
            Timer.Spawn(TimeSpan.FromSeconds(5), () => 
            {
                CheckLastManStanding(uid, component);
            });
            
            Timer.Spawn(TimeSpan.FromMinutes(2), () => 
            {
                if (!GameTicker.IsGameRuleActive(uid, gameRule))
                    return;
                
                var message = Loc.GetString("battle-royale-kill-or-be-killed");
                var title = Loc.GetString("battle-royale-title");
            
                var sound = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");
            
                _chatSystem.DispatchGlobalAnnouncement(message, title, true, sound, Color.Red);
            });
        }

        private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
        {
            var query = EntityQueryEnumerator<BattleRoyaleRuleComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var br, out var gameRule))
            {
                if (!GameTicker.IsGameRuleActive(uid, gameRule))
                    continue;

                var newMind = _mind.CreateMind(ev.Player.UserId, ev.Profile.Name);
                _mind.SetUserId(newMind, ev.Player.UserId);

                var mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(ev.Station, null, ev.Profile);
                DebugTools.AssertNotNull(mobMaybe);
                var mob = mobMaybe!.Value;
				
				if (HasComp<PainNumbnessComponent>(mob))
					RemComp<PainNumbnessComponent>(mob);
				
				if (HasComp<MoodModifyTraitComponent>(mob))
					RemComp<MoodModifyTraitComponent>(mob);
				
				if (HasComp<PermanentBlindnessComponent>(mob))
					RemComp<PermanentBlindnessComponent>(mob);
				
				if (HasComp<NarcolepsyComponent>(mob))
					RemComp<NarcolepsyComponent>(mob);

                _mind.TransferTo(newMind, mob);
                SetOutfitCommand.SetOutfit(mob, br.Gear, EntityManager);
                EnsureComp<KillTrackerComponent>(mob);
                EnsureComp<SleepingComponent>(mob);
				
                _skills.GrantAllSkills(mob);
				
                var pacifiedComp = EnsureComp<PacifiedComponent>(mob);
                Timer.Spawn(TimeSpan.FromMinutes(2), () => 
                {
                    if (!Deleted(mob) && HasComp<PacifiedComponent>(mob))
				        RemComp<PacifiedComponent>(mob);
                });
				
                var blurryVisionComp = EnsureComp<BlurryVisionComponent>(mob);
                Timer.Spawn(TimeSpan.FromSeconds(15), () => 
                {
                    if (!Deleted(mob) && HasComp<BlurryVisionComponent>(mob))
				        RemComp<BlurryVisionComponent>(mob);
                });

                ev.Handled = true;
				
                Timer.Spawn(TimeSpan.FromSeconds(0.5), () => 
                {
                    CheckLastManStanding(uid, br);
                });
				
                break;
            }
        }

        private void OnMobStateChanged(MobStateChangedEvent args)
        {
            if (args.NewMobState != MobState.Dead)
                return;

            var query = EntityQueryEnumerator<BattleRoyaleRuleComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var br, out var gameRule))
            {
                if (!GameTicker.IsGameRuleActive(uid, gameRule))
                    continue;

                CheckLastManStanding(uid, br);
            }
        }

        private void OnKillReported(ref KillReportedEvent ev)
        {
            var query = EntityQueryEnumerator<BattleRoyaleRuleComponent, PointManagerComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var br, out var point, out var gameRule))
            {
                if (!GameTicker.IsGameRuleActive(uid, gameRule))
                    continue;

                if (ev.Primary is KillPlayerSource player)
                {
                    _point.AdjustPointValue(player.PlayerId, 1, uid, point);
                }

                if (ev.Assist is KillPlayerSource assist)
                {
                    _point.AdjustPointValue(assist.PlayerId, 0.5f, uid, point);
                }

                SendKillCallout(uid, ref ev);
            }
        }

        private void SendKillCallout(EntityUid uid, ref KillReportedEvent ev)
        {
            if (ev.Primary is KillEnvironmentSource || ev.Suicide)
            {
                var calloutNumber = _random.Next(0, MaxEnvironmentalCallouts + 1);
                var calloutId = $"death-match-kill-callout-env-{calloutNumber}";
                var victimName = GetEntityName(ev.Entity);
                var message = Loc.GetString(calloutId, ("victim", victimName));
                _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, uid, false, true, Color.OrangeRed);
                return;
            }

            string killerString;
            if (ev.Primary is KillPlayerSource primarySource)
            {
                var primaryName = GetPlayerName(primarySource.PlayerId);
                if (ev.Assist is KillPlayerSource assistSource)
                {
                    var assistName = GetPlayerName(assistSource.PlayerId);
                    killerString = Loc.GetString("death-match-assist", ("primary", primaryName), ("secondary", assistName));
                }
                else
                {
                    killerString = primaryName;
                }

                var calloutNumber = _random.Next(0, MaxNormalCallouts + 1);
                var calloutId = $"death-match-kill-callout-{calloutNumber}";
                var victimName = GetEntityName(ev.Entity);
                var message = Loc.GetString(calloutId, ("killer", killerString), ("victim", victimName));
                _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, uid, false, true, Color.OrangeRed);
            }
            else if (ev.Primary is KillNpcSource npcSource)
            {
                killerString = GetEntityName(npcSource.NpcEnt);
                var calloutNumber = _random.Next(0, MaxNormalCallouts + 1);
                var calloutId = $"death-match-kill-callout-{calloutNumber}";
                var victimName = GetEntityName(ev.Entity);
                var message = Loc.GetString(calloutId, ("killer", killerString), ("victim", victimName));
                _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, uid, false, true, Color.OrangeRed);
            }
        }

        private string GetPlayerName(NetUserId userId)
        {
            if (!_player.TryGetSessionById(userId, out var session))
                return "Unknown";

            if (session.AttachedEntity == null)
                return session.Name;

            return Loc.GetString("death-match-name-player",
                ("name", MetaData(session.AttachedEntity.Value).EntityName),
                ("username", session.Name));
        }

        private string GetEntityName(EntityUid entity)
        {
            if (TryComp<ActorComponent>(entity, out var actor))
            {
                return Loc.GetString("death-match-name-player",
                    ("name", MetaData(entity).EntityName),
                    ("username", actor.PlayerSession.Name));
            }

            return Loc.GetString("death-match-name-npc",
                ("name", MetaData(entity).EntityName));
        }

        private void CheckLastManStanding(EntityUid uid, BattleRoyaleRuleComponent component)
        {
            var alivePlayers = GetAlivePlayers();

            if (alivePlayers.Count == 1)
            {
                if (!component.WinnerAnnounced || component.Victor == null || component.Victor.Value != alivePlayers.First())
                {
                    component.Victor = alivePlayers.First();
                    if (!component.WinnerAnnounced && _mind.TryGetMind(component.Victor.Value, out var mindId, out var mind))
                    {
                        component.WinnerAnnounced = true;
                        var victorName = MetaData(component.Victor.Value).EntityName;
                        var playerName = mind.Session?.Name ?? victorName;
                        if (Timing.CurTime < TimeSpan.FromSeconds(10))
                        {
                            _chatManager.DispatchServerAnnouncement(
                                Loc.GetString("battle-royale-single-player", ("player", playerName)));
                        }
                        else
                        {
                            _chatManager.DispatchServerAnnouncement(
                                Loc.GetString("battle-royale-winner-announcement", ("player", playerName)));
                        }
                        Timer.Spawn(component.RoundEndDelay, () =>
                        {
                            if (GameTicker.RunLevel == GameRunLevel.InRound)
                                _roundEnd.EndRound();
                        });
                    }
                }
            }
            else if (alivePlayers.Count == 0)
            {
                component.Victor = null;
                _roundEnd.EndRound();
            }
        }

        private void OnPlayerDetached(PlayerDetachedEvent ev)
        {
            var query = EntityQueryEnumerator<BattleRoyaleRuleComponent, GameRuleComponent>();
            while (query.MoveNext(out var uid, out var br, out var gameRule))
            {
                if (!GameTicker.IsGameRuleActive(uid, gameRule))
                    continue;
                CheckLastManStanding(uid, br);
            }
        }

        private List<EntityUid> GetAlivePlayers()
        {
            var result = new List<EntityUid>();
            var mobQuery = EntityQueryEnumerator<MobStateComponent, ActorComponent>();

            while (mobQuery.MoveNext(out var uid, out var mobState, out var actor))
            {
                if (HasComp<GhostBarPlayerComponent>(uid) || HasComp<IsDeadICComponent>(uid))
                    continue;

                if (actor.PlayerSession?.Status != SessionStatus.Connected &&
                    actor.PlayerSession?.Status != SessionStatus.InGame)
                    continue;

                if (_mobState.IsAlive(uid, mobState))
                    result.Add(uid);
            }

            return result;
        }

        protected override void AppendRoundEndText(EntityUid uid,
            BattleRoyaleRuleComponent component,
            GameRuleComponent gameRule,
            ref RoundEndTextAppendEvent args)
        {
            if (!TryComp<PointManagerComponent>(uid, out var point))
                return;

            if (component.Victor != null && _mind.TryGetMind(component.Victor.Value, out var victorMindId, out var victorMind))
            {
                var victorName = MetaData(component.Victor.Value).EntityName;
                var victorPlayerName = victorMind.Session?.Name ?? victorName;
                args.AddLine(Loc.GetString("battle-royale-winner", ("player", victorPlayerName)));
                args.AddLine("");
            }

            args.AddLine(Loc.GetString("battle-royale-scoreboard-header"));
            args.AddLine(new FormattedMessage(point.Scoreboard).ToMarkup());
        }
    }
}

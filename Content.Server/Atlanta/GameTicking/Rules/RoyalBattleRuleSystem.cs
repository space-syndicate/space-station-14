using Content.Server.Administration.Commands;
using Content.Server.Atlanta.GameTicking.Rules.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Robust.Server.Player;
using Robust.Shared.Utility;

namespace Content.Server.Atlanta.GameTicking.Rules;

public sealed class RoyalBattleRuleSystem : GameRuleSystem<RoyalBattleRuleComponent>
{
    /// <inheritdoc/>
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReport);
    }

    private void OnKillReport(ref KillReportedEvent ev)
    {
        var query = EntityQueryEnumerator<RoyalBattleRuleComponent>();
        while (query.MoveNext(out var uid, out var rb))
        {
            rb.PlayersCount--;
            _chatManager.DispatchServerAnnouncement($"Осталось {rb.PlayersCount} претендентов на приз!", Color.Red);
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

            rb.Players.Add(mob);
            rb.PlayersCount++;

            ev.Handled = true;
            break;
        }
    }
}

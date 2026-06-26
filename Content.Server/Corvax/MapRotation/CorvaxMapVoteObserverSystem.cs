using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.Database;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.MapRotation;

public sealed partial class CorvaxMapVoteObserverSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IVoteManager _voteManager = default!;

    private readonly HashSet<int> _subscribedVotes = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var vote in _voteManager.ActiveVotes)
        {
            if (_subscribedVotes.Contains(vote.Id))
                continue;

            if (!TryGetMapVoteOptions(vote, out _))
                continue;

            _subscribedVotes.Add(vote.Id);
            vote.OnFinished += OnMapVoteFinished;
            vote.OnCancelled += OnMapVoteCancelled;
        }
    }

    private void OnMapVoteCancelled(IVoteHandle sender)
    {
        _subscribedVotes.Remove(sender.Id);
    }

    private void OnMapVoteFinished(IVoteHandle sender, VoteFinishedEventArgs args)
    {
        _subscribedVotes.Remove(sender.Id);

        if (!TryGetMapVoteOptions(sender, out var eligibleMaps))
            return;

        var votePicked = GetVotePickedMap(args, eligibleMaps);
        if (votePicked == null)
            return;

        var finalSelectedMap = votePicked;
        var rareRotationApplied = false;

        var mapRotation = EntityManager.System<CorvaxMapRotationSystem>();
        if (mapRotation.TryGetRareMap(eligibleMaps, out var rareMap))
        {
            finalSelectedMap = rareMap;
            rareRotationApplied = finalSelectedMap != votePicked;
        }

        EntityManager.System<CorvaxMapVoteStatsSystem>().RecordMapVoteResult(
            eligibleMaps,
            sender.VotesPerOption,
            votePicked,
            finalSelectedMap,
            rareRotationApplied);

        if (!rareRotationApplied)
            return;

        if (_gameTicker.CanUpdateMap() && _gameMapManager.CheckMapExists(finalSelectedMap.ID))
        {
            _gameMapManager.SelectMap(finalSelectedMap.ID);
            _gameTicker.UpdateInfoText();
        }

        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(
                "ui-vote-map-rare-rotation",
                ("winner", votePicked.MapName),
                ("picked", finalSelectedMap.MapName)));
        _adminLogger.Add(
            LogType.Vote,
            LogImpact.Medium,
            $"Corvax rare map rotation overrode map vote result {votePicked.ID} with {finalSelectedMap.ID}");
    }

    private bool TryGetMapVoteOptions(IVoteHandle vote, out GameMapPrototype[] maps)
    {
        maps = vote.VotesPerOption.Keys
            .OfType<GameMapPrototype>()
            .OrderBy(map => map.ID)
            .ToArray();

        return maps.Length > 0 && maps.Length == vote.VotesPerOption.Count;
    }

    private GameMapPrototype? GetVotePickedMap(VoteFinishedEventArgs args, GameMapPrototype[] eligibleMaps)
    {
        if (args.Winner is GameMapPrototype winner)
            return winner;

        var tiedMaps = args.Winners
            .OfType<GameMapPrototype>()
            .Select(map => map.ID)
            .ToHashSet();

        if (tiedMaps.Count == 0)
            return null;

        var selectedMap = _gameMapManager.GetSelectedMap();
        if (selectedMap != null && tiedMaps.Contains(selectedMap.ID))
            return selectedMap;

        return eligibleMaps
            .Where(map => tiedMaps.Contains(map.ID))
            .OrderBy(map => map.ID)
            .FirstOrDefault();
    }
}

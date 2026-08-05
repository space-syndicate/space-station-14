using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.Database;
using Content.Shared.Maps;
using Robust.Shared.Asynchronous;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.MapRotation;

public sealed partial class CorvaxMapVoteObserverSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private IVoteManager _voteManager = default!;

    private readonly HashSet<int> _subscribedVotes = new();

    public override void Initialize()
    {
        base.Initialize();

        _voteManager.VoteCreating += OnVoteCreating;
        _voteManager.VoteCreated += OnVoteCreated;

        foreach (var vote in _voteManager.ActiveVotes)
            OnVoteCreated(vote);
    }

    public override void Shutdown()
    {
        _voteManager.VoteCreating -= OnVoteCreating;
        _voteManager.VoteCreated -= OnVoteCreated;

        foreach (var vote in _voteManager.ActiveVotes)
        {
            if (!_subscribedVotes.Contains(vote.Id))
                continue;

            vote.OnFinished -= OnMapVoteFinished;
            vote.OnCancelled -= OnMapVoteCancelled;
        }

        _subscribedVotes.Clear();
        base.Shutdown();
    }

    private void OnVoteCreating(VoteOptions options)
    {
        var maps = options.Options
            .Select(option => option.data)
            .OfType<GameMapPrototype>()
            .ToArray();

        if (maps.Length == 0 || maps.Length != options.Options.Count)
            return;

        var mapRotation = EntityManager.System<CorvaxMapRotationSystem>();
        var allowedMaps = mapRotation.FilterVoteMaps(maps)
            .Select(map => map.ID)
            .ToHashSet();
        options.Options.RemoveAll(option =>
            option.data is GameMapPrototype map && !allowedMaps.Contains(map.ID));

        var previousModifier = options.VoteCountModifier;
        options.VoteCountModifier = (option, currentVotes) =>
        {
            var votes = previousModifier?.Invoke(option, currentVotes) ?? currentVotes;
            return option is GameMapPrototype map
                ? mapRotation.GetDisplayedVoteCount(map.ID, votes)
                : votes;
        };
    }

    private void OnVoteCreated(IVoteHandle vote)
    {
        if (_subscribedVotes.Contains(vote.Id) || !TryGetMapVoteOptions(vote, out _))
            return;

        _subscribedVotes.Add(vote.Id);
        vote.OnFinished += OnMapVoteFinished;
        vote.OnCancelled += OnMapVoteCancelled;
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

        var mapRotation = EntityManager.System<CorvaxMapRotationSystem>();
        if (mapRotation.TryGetRotationMap(eligibleMaps, sender.VotesPerOption, votePicked, out var rotationMap))
            finalSelectedMap = rotationMap;

        var selectionOverridden = finalSelectedMap != votePicked;
        var rareRotationApplied = selectionOverridden && mapRotation.UsesPeriodicRareStrategy;

        mapRotation.RecordMapVoteResult(
            eligibleMaps,
            sender.VotesPerOption,
            votePicked,
            finalSelectedMap,
            rareRotationApplied);

        if (!selectionOverridden)
            return;

        // VoteCreated fires before the standard map vote attaches its completion callback.
        // Apply the override after all synchronous vote handlers have selected the regular winner.
        _taskManager.RunOnMainThread(() => ApplyRotationOverride(votePicked, finalSelectedMap, rareRotationApplied));
    }

    private void ApplyRotationOverride(
        GameMapPrototype votePicked,
        GameMapPrototype finalSelectedMap,
        bool rareRotationApplied)
    {
        if (_gameTicker.CanUpdateMap() && _gameMapManager.CheckMapExists(finalSelectedMap.ID))
        {
            _gameMapManager.SelectMap(finalSelectedMap.ID);
            _gameTicker.UpdateInfoText();
        }

        _chatManager.DispatchServerAnnouncement(Loc.GetString(
            "ui-vote-map-rare-rotation",
            ("picked", finalSelectedMap.MapName)));
        var reason = rareRotationApplied ? " using periodic rare rotation" : string.Empty;
        _adminLogger.Add(
            LogType.Vote,
            LogImpact.Medium,
            $"Corvax map rotation overrode map vote result {votePicked.ID} with {finalSelectedMap.ID}{reason}");
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

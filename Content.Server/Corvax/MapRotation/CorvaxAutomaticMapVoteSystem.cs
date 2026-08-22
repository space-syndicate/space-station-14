using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Voting;
using Robust.Server.Player;
using Robust.Shared.Configuration;

namespace Content.Server.Corvax.MapRotation;

public sealed partial class CorvaxAutomaticMapVoteSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IVoteManager _voteManager = default!;

    private bool _startedThisLobby;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        TryStartAutomaticMapVote();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        _startedThisLobby = false;
    }

    private void TryStartAutomaticMapVote()
    {
        if (_startedThisLobby ||
            !_gameTicker.LobbyEnabled ||
            _gameTicker.RunLevel != GameRunLevel.PreRoundLobby ||
            _playerManager.PlayerCount == 0 ||
            _voteManager.ActiveVotes.Any() ||
            !_cfg.GetCVar(CCCVars.MapRotationEnabled))
        {
            return;
        }

        _startedThisLobby = true;
        _voteManager.CreateStandardVote(null, StandardVoteType.Map);
    }
}

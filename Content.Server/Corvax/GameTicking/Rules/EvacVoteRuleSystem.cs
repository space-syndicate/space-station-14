using Content.Server.RoundEnd;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Corvax.GameTicking.Rules.Components;
using Content.Server.Voting.Managers;
using Content.Server.Voting;
using Content.Shared.Voting;
using Robust.Shared.Timing;
using Content.Shared.GameTicking.Components;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared.Configuration;
using System.Reflection;

namespace Content.Server.Corvax.GameTicking.Rules;

public sealed class EvacVoteRuleSystem : GameRuleSystem<EvacVoteRuleComponent>
{
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public bool isVoteActive = false;

    protected override void ActiveTick (EntityUid uid, EvacVoteRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var RoundTime = (float) _gameTicker.RoundDuration().TotalSeconds;
        var FirstVoteTime = _cfg.GetCVar(CCCVars.EvacVoteTime);
        var VoteInterval = _cfg.GetCVar(CCCVars.EvacVoteInterval);

        if(RoundTime > FirstVoteTime && component.VoteCounts == 0 && isVoteActive != true) 
        {
            CallVote(component);
            return;
        }

        if(RoundTime > FirstVoteTime + VoteInterval * component.VoteCounts && component.VoteCounts > 0 && isVoteActive != true) 
            CallVote(component);
    }

    private void CallVote(EvacVoteRuleComponent component)
    {

        var options = new VoteOptions()
        {
            InitiatorText = Loc.GetString("vote-options-server-initiator-text"),
            Title = Loc.GetString("ui-vote-type-evac"),
            Duration = TimeSpan.FromSeconds(60),
            Options =
            {
                (Loc.GetString("ui-vote-evac-button-yes"), "yes"),
                (Loc.GetString("ui-vote-auto-button-no"), "no"),
            }
        };

        var vote = _voteManager.CreateVote(options);
        isVoteActive = true;

        vote.OnFinished += (_, _) =>
            {
                var votesYes = vote.VotesPerOption["yes"];
                var votesNo = vote.VotesPerOption["no"];
                var total = votesYes + votesNo;

                var ratioRequired = _cfg.GetCVar(CCCVars.EvacVoteRequiredRatio);
                if (total > 0 && votesYes / (float) total >= ratioRequired)
                {
                    EndRound();
                }
                else
                {
                    return;
                }
            };
        
        component.VoteCounts += 1;
        isVoteActive = false;
    }

    private void EndRound()
    {
        _chat.DispatchGlobalAnnouncement(Loc.GetString($"emergency-shuttle-vote-end-announce"), null, true, null, Color.Gold);
        _roundEndSystem.RequestRoundEnd(null, null, false, cantRecall: true);
    } 
    
}

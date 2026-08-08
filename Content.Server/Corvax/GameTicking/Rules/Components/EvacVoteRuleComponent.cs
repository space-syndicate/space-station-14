

namespace Content.Server.Corvax.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(EvacVoteRuleSystem))]

public sealed partial class EvacVoteRuleComponent : Component
{
    [DataField]
    public int VoteCounts = 0;
}
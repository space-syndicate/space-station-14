using Content.Shared.GameTicking;

namespace Content.Server.Corvax.PeacefulRoundEnd;

public sealed class PeacefulRoundEndSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    private void OnRoundEnded(RoundEndedEvent ev)
    {
        var query = GetEntityQuery<>();
    }
}

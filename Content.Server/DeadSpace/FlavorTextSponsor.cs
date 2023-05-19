using Content.Server.Corvax.Sponsors;
using Content.Server.GameTicking;
using Content.Server.DetailExaminable;

namespace Content.Server.DeadSpace.FlavorTextForDonate;

public sealed class FlavorTextSponsor : EntitySystem
{
    [Dependency] private readonly SponsorsManager _sponsorsManager = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_sponsorsManager.TryGetInfo(ev.Player.UserId, out var sponsor))
        {
            if (_entManager.TryGetComponent<DetailExaminableComponent>(ev.Mob, out var Detail))
            {
                _entManager.RemoveComponent<DetailExaminableComponent>(ev.Mob);
            }
        }
    }
}
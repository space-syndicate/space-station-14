using Content.Server.Antag;
using Content.Shared.Mind.Components;
using Robust.Server.Player;

namespace Content.Server.Corvax.Antag.Briefing;

public sealed class ManualBriefingSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antagSelectionSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ManualBriefingComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ManualBriefingComponent, ComponentStartup>(OnComponentAdd);
    }

    private void OnComponentAdd(EntityUid uid, ManualBriefingComponent comp, ComponentStartup args)
    {
        SendBriefing(uid, comp);
    }

    private void OnMindAdded(EntityUid uid, ManualBriefingComponent comp, MindAddedMessage args)
    {
        SendBriefing(args.Mind, comp);
    }

    private void SendBriefing(EntityUid uid, ManualBriefingComponent comp)
    {
        if (!comp.Enabled || comp.Triggered)
            return;

        _antagSelectionSystem.SendBriefing(uid, comp.Text, comp.TextColor, comp.Sound);

        if (comp.OnceActivated)
            comp.Triggered = true;
    }
}

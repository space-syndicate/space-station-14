using System.Linq;
using System.Threading;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Robust.Shared.Player;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.StationEvents.Events;

public sealed class GreenshiftRule : StationEventSystem<GreenshiftRuleComponent>
{
    [Dependency] private readonly EventManagerSystem _event = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    protected override void Started(EntityUid uid, GreenshiftRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        Timer.Spawn(component.RoundStartAnnouncementDelay, () => AnnounceGreenshift(uid, component,  gameRule, args), component.TimerCancel);
    }

    protected override void Ended(EntityUid uid, GreenshiftRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
    }

    private void AnnounceGreenshift(EntityUid uid, GreenshiftRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (component.EnableAnnouncement) {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("station-event-greenshift"), playSound: true, colorOverride: Color.Green);
            if (component.AnnounceAudio != null)
                Audio.PlayGlobal(component.AnnounceAudio, Filter.Broadcast(), true);
        }
        component.TimerCancel = new CancellationToken();
    }
}

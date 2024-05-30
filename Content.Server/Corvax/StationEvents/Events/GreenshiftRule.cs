// _chat.DispatchGlobalAnnouncement(Loc.GetString("station-event-greenshift"), playSound: true, colorOverride: Color.Green);
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using JetBrains.Annotations;

namespace Content.Server.StationEvents.Events;

[UsedImplicitly]
public sealed class GreenshiftRule : StationEventSystem<GreenshiftRuleComponent>
{
    [Dependency] private readonly EventManagerSystem _event = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    protected override void Started(EntityUid uid, GreenshiftRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.EnableAnnouncement) {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("station-event-greenshift"), playSound: false, colorOverride: Color.Green);
            if (component.AnnounceAudio != null)
                Audio.PlayGlobal(component.AnnounceAudio, Filter.Broadcast(), true);
        }
    }
}

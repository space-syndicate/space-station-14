using Content.Server.StationEvents.Events;
using Robust.Shared.Audio;
using System.Threading;

namespace Content.Server.StationEvents.Components;

[RegisterComponent]
public sealed partial class GreenshiftRuleComponent : Component
{
    [DataField]
    public bool EnableAnnouncement;

    [DataField]
    public SoundSpecifier? AnnounceAudio;

    [DataField("roundStartAnnouncementDelay")]
    public int RoundStartAnnouncementDelay = 2*60000; // 2 minutes in milliseconds

    public CancellationToken TimerCancel = new();
}

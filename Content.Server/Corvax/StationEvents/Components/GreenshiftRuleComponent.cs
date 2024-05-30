using Content.Server.StationEvents.Events;
using Robust.Shared.Audio;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(FalseAlarmRule))]
public sealed partial class GreenshiftRuleComponent : Component
{
    [DataField]
    public bool EnableAnnouncement;

    [DataField]
    public SoundSpecifier? AnnounceAudio;
}

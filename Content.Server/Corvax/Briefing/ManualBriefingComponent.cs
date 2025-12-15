using Robust.Shared.Audio;

namespace Content.Server.Corvax.Antag.Briefing;

[RegisterComponent]
public sealed partial class ManualBriefingComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public string Text = string.Empty;

    [DataField]
    public Color? TextColor = Color.Gold;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    /// <summary>
    /// Briefing is activated once during the entire time
    /// </summary>
    [DataField]
    public bool OnceActivated = true;

    [DataField][ViewVariables]
    public bool Triggered = false;
}

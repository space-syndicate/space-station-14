// Inspired by Nyanotrasen

namespace Content.Server.HyperLink;

[RegisterComponent]
public sealed partial class HyperLinkComponent : Component
{
    [DataField("url")]
    [ViewVariables(VVAccess.ReadWrite)]
    public string URL = string.Empty;
}
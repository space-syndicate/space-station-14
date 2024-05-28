// Inspired by Nyanotrasen

namespace Content.Server.HyperLinkBook;

[RegisterComponent]
public sealed partial class HyperLinkBookComponent : Component
{
    [DataField("url")]
    [ViewVariables(VVAccess.ReadWrite)]
    public string URL = string.Empty;
}
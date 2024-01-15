using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.HiddenDescription;

/// <summary>
/// 
/// </summary>

[RegisterComponent, Access(typeof(HiddenDescriptionSystem))]
public sealed partial class HiddenDescriptionComponent : Component
{
    /// <summary>
    /// Data that is only revealed to players with certain tags or components
    /// </summary>
    [DataField]
    public Dictionary<string, EntityWhitelist> MindWhitelistData = new();

    /// <summary>
    /// Data that is only revealed to players of a specific role
    /// </summary>
    [DataField]
    public Dictionary<string, List<ProtoId<JobPrototype>>> JobData = new();
}

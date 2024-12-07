using Content.Shared.Security;

namespace Content.Server.CartridgeLoader.Cartridges;

[RegisterComponent, Access(typeof(SecWatchCartridgeSystem))]
public sealed partial class SecWatchCartridgeComponent : Component
{
    /// <summary>
    /// Only show people with these statuses.
    /// </summary>
    [DataField]
    public List<SecurityStatus> Statuses = new()
    {
        SecurityStatus.Wanted,
        SecurityStatus.Detained
    };

    /// <summary>
    /// Station entity thats getting its records checked.
    /// </summary>
    [DataField]
    public EntityUid? Station;
}

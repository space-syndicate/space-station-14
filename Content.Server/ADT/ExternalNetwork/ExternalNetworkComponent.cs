using Content.Shared.DeviceNetwork;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.ADT.ExternalNetwork;

[RegisterComponent]
[ComponentProtoName("ExternalNetworkConnection")]
public sealed class ExternalNetworkComponent : Component
{

    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("address")]
    public string Address { get; set; } = String.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("deviceType")]
    public string DeviceType { get; set; } = DeviceTypes.Fax;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("autoConnect")]
    public bool AutoConnect = true;

    [ViewVariables]
    public List<string> KnownHosts { get; } = new();

}

public static class DeviceTypes
{
    public static readonly string Fax = "Fax";
    public static readonly string Teleporter = "Teleporter";
}

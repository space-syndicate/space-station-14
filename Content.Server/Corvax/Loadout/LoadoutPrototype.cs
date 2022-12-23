using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Corvax.Loadout;

[Prototype("loadout")]
public sealed class LoadoutItemPrototype : IPrototype
{
    [IdDataFieldAttribute] public string ID { get; } = default!;

    [DataField("entity", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string EntityId { get; } = String.Empty;
    
    [DataField("slot")]
    public string SlotId { get; } = String.Empty;
    
    [DataField("whitelistJobs", customTypeSerializer: typeof(PrototypeIdListSerializer<JobPrototype>))]
    public List<string> WhitelistJobs { get; } = new();
    
    [DataField("blacklistJobs", customTypeSerializer: typeof(PrototypeIdListSerializer<JobPrototype>))]
    public List<string> BlacklistJobs { get; } = new();
}

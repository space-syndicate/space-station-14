using Content.Server.StationEvents.Events;
using Content.Shared.Storage;

namespace Content.Server.Corvax.EvilTwin;

[RegisterComponent, Access(typeof(EvilTwinSpawnRule))]
public sealed partial class EvilTwinSpawnRuleComponent : Component
{
    [DataField("entries")]
    public List<EntitySpawnEntry> Entries = new();

    /// <summary>
    /// At least one special entry is guaranteed to spawn
    /// </summary>
    [DataField("specialEntries")]
    public List<EntitySpawnEntry> SpecialEntries = new();
}

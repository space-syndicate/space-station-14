using System.Text.Json.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Server.GuideGenerator;

public sealed class EntityEntry
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("desc")]
    public string Description { get; }

    [JsonPropertyName("suffix")]
    public string Suffix { get; }

    public EntityEntry(EntityPrototype proto)
    {
        Id = proto.ID;
        Name = TextTools.TextTools.CapitalizeString(proto.Name); // Corvax-Wiki
        Description = proto.Description;
        Suffix = proto.EditorSuffix != null ? proto.EditorSuffix : "";
    }
}

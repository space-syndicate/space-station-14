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

    public EntityEntry(EntityPrototype proto)
    {
        Id = proto.ID;
        if (proto.Name.Length > 1)
        {
            Name = char.ToUpper(proto.Name[0]) + proto.Name.Remove(0, 1);
        }
        else if (proto.Name.Length == 1)
        {
            Name = char.ToUpper(proto.Name[0]).ToString(); // xD
        }
        else
        {
            Name = proto.Name;
        }
        Description = proto.Description;
    }
}

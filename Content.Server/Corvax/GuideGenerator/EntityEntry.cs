using System.Linq;
using System.Text.Json.Serialization;
using Robust.Shared.Prototypes;
using Content.Shared.Labels.Components;

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

    [JsonPropertyName("label")]
    public string Label { get; }

    public EntityEntry(EntityPrototype proto)
    {
        Id = proto.ID;
        Name = TextTools.TextTools.CapitalizeString(proto.Name); // Corvax-Wiki
        Description = proto.Description;
        Suffix = proto.EditorSuffix != null ? proto.EditorSuffix : "";

        Label = proto.Components.Values
            .Select(x => x.Component)
            .OfType<LabelComponent>()
            .Select(lc => lc.CurrentLabel)
            .Where(label => !string.IsNullOrEmpty(label))
            .Select(label => Loc.GetString(label!))
            .FirstOrDefault() ?? "";
    }
}

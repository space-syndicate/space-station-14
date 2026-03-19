using System.Linq;
using System.Text.Json.Serialization;
using Content.Shared.Labels.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

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
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var loc = IoCManager.Resolve<ILocalizationManager>();

        Id = proto.ID;
        Name = TextTools.CapitalizeString(TextTools.GetDisplayName(proto, prototypeManager, loc));
        Description = proto.Description;
        Suffix = proto.EditorSuffix ?? "";

        Label = proto.Components.Values
            .Select(x => x.Component)
            .OfType<LabelComponent>()
            .Select(lc => lc.CurrentLabel)
            .Where(label => !string.IsNullOrEmpty(label))
            .Select(label => Loc.GetString(label!))
            .FirstOrDefault() ?? "";
    }
}

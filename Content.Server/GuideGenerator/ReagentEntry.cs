using System.Linq;
using System.Text.Json.Serialization;
using Content.Server.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server.GuideGenerator;

public sealed class ReagentEntry
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("group")]
    public string Group { get; }

    [JsonPropertyName("desc")]
    public string Description { get; }

    [JsonPropertyName("physicalDesc")]
    public string PhysicalDescription { get; }

    [JsonPropertyName("color")]
    public string SubstanceColor { get; }

    [JsonPropertyName("recipes")]
    public List<string> Recipes { get; } = new();

    [JsonPropertyName("metabolisms")]
    public Dictionary<string, ReagentEffectsEntry>? Metabolisms { get; }

    public ReagentEntry(ReagentPrototype proto)
    {
        Id = proto.ID;
        Name = TextTools.TextTools.CapitalizeString(proto.LocalizedName); // Corvax-Wiki
        Group = proto.Group;
        Description = proto.LocalizedDescription;
        PhysicalDescription = proto.LocalizedPhysicalDescription;
        SubstanceColor = proto.SubstanceColor.ToHex();
        // Corvax-Wiki-Start
        Dictionary<ProtoId<MetabolismGroupPrototype>, ReagentEffectsEntry>? metabolisms = proto.Metabolisms;
        if (metabolisms is not null)
            Metabolisms = metabolisms.ToDictionary(x => x.Key.ToString(), x => x.Value);
        else
            Metabolisms = null;
        // Corvax-Wiki-End
    }
}

public sealed class ReactionEntry
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("reactants")]
    public Dictionary<string, ReactantEntry> Reactants { get; }

    [JsonPropertyName("products")]
    public Dictionary<string, float> Products { get; }

    [JsonPropertyName("effects")]
    public List<ReagentEffect> Effects { get; }

    public ReactionEntry(ReactionPrototype proto)
    {
        Id = proto.ID;
        Name = TextTools.TextTools.CapitalizeString(proto.Name); // Corvax-Wiki
        Reactants =
            proto.Reactants
                .Select(x => KeyValuePair.Create(x.Key, new ReactantEntry(x.Value.Amount.Float(), x.Value.Catalyst)))
                .ToDictionary(x => x.Key, x => x.Value);
        Products =
            proto.Products
                .Select(x => KeyValuePair.Create(x.Key, x.Value.Float()))
                .ToDictionary(x => x.Key, x => x.Value);
        Effects = proto.Effects;
    }
}

public sealed class ReactantEntry
{
    [JsonPropertyName("amount")]
    public float Amount { get; }

    [JsonPropertyName("catalyst")]
    public bool Catalyst { get; }

    public ReactantEntry(float amnt, bool cata)
    {
        Amount = amnt;
        Catalyst = cata;
    }
}

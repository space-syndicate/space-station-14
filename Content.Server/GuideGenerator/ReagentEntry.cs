using System.Linq;
using System.Text.Json.Serialization;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
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

    [JsonPropertyName("textColor")]
    public string TextColor { get; }

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

        var r = proto.SubstanceColor.R;
        var g = proto.SubstanceColor.G;
        var b = proto.SubstanceColor.B;
        TextColor = (0.2126f * r + 0.7152f * g + 0.0722f * b > 0.5
            ? Color.Black
            : Color.White).ToHex();
        
        Metabolisms = proto.Metabolisms?.ToDictionary(x => x.Key.Id, x => new ReagentEffectsEntry(x.Value));
    }
}

public sealed class ReactionEntry
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("reactants")]
    public Dictionary<string, ReactantEntry> Reactants { get; } = new();

    [JsonPropertyName("products")]
    public Dictionary<string, float> Products { get; }

    [JsonPropertyName("mixingCategories")]
    public List<MixingCategoryEntry> MixingCategories { get; } = new();

    [JsonPropertyName("minTemp")]
    public float MinTemp { get; }

    [JsonPropertyName("maxTemp")]
    public float MaxTemp { get; }

    [JsonPropertyName("hasMax")]
    public bool HasMax { get; }

    [JsonPropertyName("effects")]
    public List<ReagentEffectEntry> ExportEffects { get; } = new();

    [JsonIgnore]
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

        ExportEffects = proto.Effects.Select(x => new ReagentEffectEntry(x)).ToList();
        Effects = proto.Effects;
        MinTemp = proto.MinimumTemperature;
        MaxTemp = proto.MaximumTemperature;
        HasMax = !float.IsPositiveInfinity(MaxTemp);
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
public sealed class MixingCategoryEntry
{
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("id")]
    public string Id { get; }

    public MixingCategoryEntry(MixingCategoryPrototype proto)
    {
        Name = Loc.GetString(proto.VerbText);
        Id = proto.ID;
    }
}
public sealed class ReagentEffectsEntry
{
    [JsonPropertyName("rate")]
    public FixedPoint2 MetabolismRate { get; } = FixedPoint2.New(0.5f);

    [JsonPropertyName("effects")]
    public List<ReagentEffectEntry> Effects { get; } = new();

    public ReagentEffectsEntry(Content.Shared.Chemistry.Reagent.ReagentEffectsEntry proto)
    {
        MetabolismRate = proto.MetabolismRate;
        Effects = proto.Effects.Select(x => new ReagentEffectEntry(x)).ToList();
    }

}
public sealed class ReagentEffectEntry
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("description")]
    public string Description { get; }

    public ReagentEffectEntry(ReagentEffect proto)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();
        var entSys = IoCManager.Resolve<IEntitySystemManager>();

        Id = proto.GetType().Name;
        Description = GuidebookEffectDescriptionToWeb(proto.GuidebookEffectDescription(prototype, entSys) ?? "");
    }

    private string GuidebookEffectDescriptionToWeb(string guideBookText)
    {
        guideBookText = guideBookText.Replace("[", "<");
        guideBookText = guideBookText.Replace("]", ">");
        guideBookText = guideBookText.Replace("color", "span");

        while (guideBookText.IndexOf("<span=") != -1)
        {
            var first = guideBookText.IndexOf("<span=") + "<span=".Length - 1;
            var last = guideBookText.IndexOf(">", first);
            var replacementString = guideBookText.Substring(first, last - first);
            var color = replacementString.Substring(1);
            guideBookText = guideBookText.Replace(replacementString, String.Format(" style=\"color: {0};\"", color));
        }

        return guideBookText;
    }
}

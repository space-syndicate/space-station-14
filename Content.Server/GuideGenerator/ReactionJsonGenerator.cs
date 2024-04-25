using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server.GuideGenerator;

public sealed class ReactionJsonGenerator
{
    [ValidatePrototypeId<MixingCategoryPrototype>]
    private const string DefaultMixingCategory = "DummyMix";

    public static void PublishJson(StreamWriter file)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();

        var reactions =
            prototype
                .EnumeratePrototypes<ReactionPrototype>()
                .Select(x => new ReactionEntry(x))
                .ToDictionary(x => x.Id, x => x);

        // MixingCategories
        foreach (var reaction in reactions)
        {
            var reactionPrototype = prototype.Index<ReactionPrototype>(reaction.Key);
            var mixingCategories = new List<MixingCategoryPrototype>();
            if (reactionPrototype.MixingCategories != null)
            {
                foreach (var category in reactionPrototype.MixingCategories)
                {
                    mixingCategories.Add(prototype.Index(category));
                }
            }
            else
            {
                mixingCategories.Add(prototype.Index<MixingCategoryPrototype>(DefaultMixingCategory));
            }

            foreach (var mixingCategory in mixingCategories)
            {
                reactions[reaction.Key].MixingCategories.Add(new MixingCategoryEntry(mixingCategory));
            }
        }

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals, // Corvax-Wiki
            Converters =
            {
                new UniversalJsonConverter<ReagentEffect>(),
            }
        };

        file.Write(JsonSerializer.Serialize(reactions, serializeOptions));
    }
}


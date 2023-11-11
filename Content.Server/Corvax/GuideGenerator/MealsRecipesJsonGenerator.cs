using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text.Json;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Kitchen;
using Robust.Shared.Prototypes;
using Content.Shared.Construction.Prototypes;
using Content.Server.Construction.Components;
using Content.Server.Chemistry.ReactionEffects;

namespace Content.Server.GuideGenerator;

public sealed class MicrowaveMealRecipeJsonGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();
        var entities = prototype.EnumeratePrototypes<EntityPrototype>();
        var constructable = prototype.EnumeratePrototypes<ConstructionGraphPrototype>();
        var output = new Dictionary<string, dynamic>();

        var microwaveRecipes =
            prototype
                .EnumeratePrototypes<FoodRecipePrototype>()
                .Select(x => new MicrowaveRecipeEntry(x))
                .ToDictionary(x => x.Id, x => x);

        output["microwaveRecipes"] = microwaveRecipes;

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        file.Write(JsonSerializer.Serialize(output, serializeOptions));
    }
}

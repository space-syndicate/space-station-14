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


        var sliceableRecipes =
            entities
                .Where(x => x.Components.TryGetComponent("SliceableFood", out var _))
                .Select(x => new SliceRecipeEntry(x))
                .Where(x => x.Result != "") // SOMEONE THOUGHT THAT IT WOULD BE A GREAT IDEA TO PUT COMPONENT ON AN ITEM WITHOUT SPECIFYING THE OUTPUT THING.
                .Where(x => x.Count > 0) // Just in case.
                .ToDictionary(x => x.Id, x => x);


        var grindableRecipes =
            entities
                .Where(x => x.Components.TryGetComponent("Extractable", out var _))
                .Where(x => x.Components.TryGetComponent("SolutionContainerManager", out var _))
                .Where(x => (Regex.Match(x.ID.ToLower().Trim(), @".*[Ff]ood*").Success)) // we dont need some "organ" or "pills" prototypes.
                .Select(x => new GrindRecipeEntry(x))
                .ToDictionary(x => x.Id, x => x);

        output["microwaveRecipes"] = microwaveRecipes;
        output["sliceableRecipes"] = sliceableRecipes;
        output["grindableRecipes"] = grindableRecipes;

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        file.Write(JsonSerializer.Serialize(output, serializeOptions));
    }
}

using Content.Server.Chemistry.ReagentEffects;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using System.IO;
using System.Linq;
using System.Text.Json;
using static Content.Server.GuideGenerator.ChemistryJsonGenerator;

namespace Content.Server.Corvax.GuideGenerator;
public sealed class HealthChangeReagentsJsonGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();

        Dictionary<string, Dictionary<string, HashSet<string>>> healthChangeReagents = new();

        foreach (var reagent in prototype.EnumeratePrototypes<ReagentPrototype>())
        {
            if (reagent.Metabolisms is null) continue;

            foreach (var metabolism in reagent.Metabolisms)
            {
                foreach (HealthChange effect in metabolism.Value.Effects.Where(x => x is HealthChange))
                {
                    foreach (var damage in effect.Damage.DamageDict)
                    {
                        var damageType = damage.Key;
                        var damageChangeType = damage.Value.Float() < 0 ? "health" : "damage";

                        if (!healthChangeReagents.ContainsKey(damageType))
                        {
                            healthChangeReagents.Add(damageType, new());
                        }

                        if (!healthChangeReagents[damageType].ContainsKey(damageChangeType))
                        {
                            healthChangeReagents[damageType].Add(damageChangeType, new());
                        }

                        healthChangeReagents[damageType][damageChangeType].Add(reagent.ID);
                    }
                }
            }
        }

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        file.Write(JsonSerializer.Serialize(healthChangeReagents, serializeOptions));
    }
}


using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public sealed class TextTools
{
    /// <summary>
    /// Capitalizes first letter of given string.
    /// </summary>
    /// <param name="str">String to capitalize</param>
    /// <returns>String with capitalized first letter</returns>
    public static string CapitalizeString(string str)
    {
        return str.Length switch
        {
            > 1 => char.ToUpper(str[0]) + str.Remove(0, 1),
            1 => char.ToUpper(str[0]).ToString(),
            _ => str
        };
    }

    public static string GetDisplayName(EntityPrototype proto, IPrototypeManager prototypeManager, ILocalizationManager loc)
    {
        foreach (var (id, parentProto) in prototypeManager.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
        {
            if (parentProto == null)
                continue;

            var name = parentProto?.Name ?? loc.GetEntityData(id).Name;
            if (!string.IsNullOrEmpty(name))
                return name;
        }
        return proto.Name;
    }

    public static string? GetEditorSuffix(EntityPrototype proto, IPrototypeManager prototypeManager, ILocalizationManager loc)
    {
        foreach (var (id, parentProto) in prototypeManager.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
        {
            if (parentProto == null)
                continue;

            var suffix = parentProto?.EditorSuffix ?? loc.GetEntityData(id).Suffix;
            if (!string.IsNullOrEmpty(suffix))
                return suffix;
        }
        return proto.EditorSuffix;
    }
}

using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public sealed class TextTools
{
    /// <summary>
    /// Capitalizes first letter of given string.
    /// </summary>
    public static string CapitalizeString(string str)
    {
        return str.Length switch
        {
            > 1 => char.ToUpper(str[0]) + str.Remove(0, 1),
            1 => char.ToUpper(str[0]).ToString(),
            _ => str
        };
    }

    /// <summary>
    /// Converts the first character of the given string to lowercase.
    /// </summary>
    public static string DecapitalizeString(string str)
    {
        return str.Length switch
        {
            > 1 => char.ToLower(str[0]) + str.Remove(0, 1),
            1 => char.ToLower(str[0]).ToString(),
            _ => str
        };
    }
    public static string GetDisplayName(EntityPrototype proto, IPrototypeManager prototypeManager, ILocalizationManager loc)
    {
        foreach (var (_, parentProto) in prototypeManager.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
        {
            if (parentProto == null)
                continue;

            var name = parentProto.Name;
            if (!string.IsNullOrEmpty(name))
                return name;
        }
        return proto.Name;
    }
}

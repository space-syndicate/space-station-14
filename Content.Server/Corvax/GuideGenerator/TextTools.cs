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
        var visited = new HashSet<string>();
        var stack = new Stack<string>();
        stack.Push(proto.ID);

        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!visited.Add(id))
                continue;

            if (!prototypeManager.TryIndex<EntityPrototype>(id, out var current))
                continue;

            if (!string.IsNullOrEmpty(current.Name))
                return current.Name;

            var parents = current.Parents;
            if (parents == null || parents.Length == 0)
                continue;

            for (var i = parents.Length - 1; i >= 0; i--)
            {
                stack.Push(parents[i]);
            }
        }

        return proto.Name;
    }
}

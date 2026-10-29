using System.IO;
using System.Linq;
using Content.Shared.Corvax.GuideGenerator;

namespace Content.Server.Corvax.GuideGenerator;

public static class EntityProjectGenerator
{
    public static HashSet<string> GetProjectEntityIds()
    {
        return EntityProjectHelper.GetProjectEntityIds();
    }

    public static void PublishJson(Stream stream)
    {
        var ids = GetProjectEntityIds();
        if (ids.Count == 0)
            return;

        var sorted = ids.ToList();
        sorted.Sort(StringComparer.Ordinal);

        GuideJson.Write(stream, sorted);
    }
}

using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Content.Shared.Corvax.GuideGenerator;

namespace Content.Server.Corvax.GuideGenerator;

public static class EntityProjectGenerator
{
    public static HashSet<string> GetProjectEntityIds()
    {
        return EntityProjectHelper.GetProjectEntityIds();
    }

    public static void PublishJson(StreamWriter file)
    {
        var ids = GetProjectEntityIds();
        if (ids.Count == 0)
            return;

        var sorted = ids.ToList();
        sorted.Sort(StringComparer.Ordinal);

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        file.Write(JsonSerializer.Serialize(sorted, serializeOptions));
    }
}

using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Shared.Corvax.GuideGenerator;

public static class EntityProjectHelper
{
    private static readonly ResPath PrototypesRoot = new("/Prototypes/");

    public static HashSet<string> GetProjectEntityIds()
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var res = IoCManager.Resolve<IResourceManager>();
        var projectFolderPrefix = cfg.GetCVar(CCVars.EntityProjectFolderPrefix);
        var excludedCoreProjectFolder = cfg.GetCVar(CCVars.EntityProjectExcludedCoreProjectFolder);

        if (string.IsNullOrWhiteSpace(projectFolderPrefix))
            return [];

        var ids = new HashSet<string>();

        foreach (var path in res.ContentFindFiles(PrototypesRoot))
        {
            if (!IsPrototypeFile(path))
                continue;

            if (!IsInIncludedProjectFolder(path, projectFolderPrefix, excludedCoreProjectFolder))
                continue;

            ExtractIdsFromYaml(res, path, ids);
        }

        return ids;
    }

    public static bool MatchesAllowedIds(string prototypeId, IReadOnlySet<string> allowedIds)
    {
        return allowedIds.Count == 0 || allowedIds.Contains(prototypeId);
    }

    private static bool IsPrototypeFile(ResPath path)
    {
        return path.Extension.Equals("yml", StringComparison.OrdinalIgnoreCase) ||
               path.Extension.Equals("yaml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInIncludedProjectFolder(
        ResPath path,
        string projectFolderPrefix,
        string excludedCoreProjectFolder)
    {
        var normalizedExcluded = excludedCoreProjectFolder.Trim();

        foreach (var part in GetPathParts(path))
        {
            if (!part.StartsWith(projectFolderPrefix, StringComparison.Ordinal))
                continue;

            if (!string.IsNullOrWhiteSpace(normalizedExcluded) &&
                part.Equals(normalizedExcluded, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetPathParts(ResPath path)
    {
        foreach (var part in path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return part;
        }
    }

    private static void ExtractIdsFromYaml(
        IResourceManager res,
        ResPath path,
        HashSet<string> output)
    {
        using var reader = res.ContentFileReadText(path);
        foreach (var document in DataNodeParser.ParseYamlStream(reader))
        {
            if (document.Root is not SequenceDataNode sequence)
                continue;

            foreach (var node in sequence.Sequence)
            {
                if (node is not MappingDataNode mapping)
                    continue;

                if (!mapping.TryGet<ValueDataNode>("type", out var typeNode) ||
                    !string.Equals(typeNode.Value, "entity", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!mapping.TryGet<ValueDataNode>("id", out var idNode) ||
                    string.IsNullOrWhiteSpace(idNode.Value))
                {
                    continue;
                }

                output.Add(idNode.Value);
            }
        }
    }
}

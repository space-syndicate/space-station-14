using Robust.Packaging;
using Robust.Packaging.AssetProcessing;

namespace Content.Packaging;

public static class ContentPackaging
{
    public static async Task WriteResources(
        string contentDir,
        AssetPass pass,
        IPackageLogger logger,
        CancellationToken cancel)
    {
        var graph = new RobustClientAssetGraph();
        pass.Dependencies.Add(new AssetPassDependency(graph.Output.Name));

        AssetGraph.CalculateGraph(graph.AllPasses.Append(pass).ToArray(), logger);

        var inputPass = graph.Input;

        await RobustClientPackaging.WriteContentAssemblies(
            inputPass,
            contentDir,
            "Content.Client",
            new[] { "Content.Client", "Content.Shared", "Content.Shared.Database", "Content.Corvax.Interfaces.Client", "Content.Corvax.Interfaces.Shared" }, // Corvax-Secrets: Add Corvax interfaces to Magic ACZ
            cancel);

        await RobustClientPackaging.WriteClientResources(contentDir, inputPass, cancel);

        inputPass.InjectFinished();
    }
}

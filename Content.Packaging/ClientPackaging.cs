using System.Diagnostics;
using System.IO.Compression;
using Robust.Packaging;
using Robust.Packaging.AssetProcessing;
using Robust.Packaging.AssetProcessing.Passes;
using Robust.Packaging.Utility;
using Robust.Shared.Timing;

namespace Content.Packaging;

public static class ClientPackaging
{
    private static readonly bool UseSecrets = File.Exists(Path.Combine("Secrets", "CorvaxSecrets.sln")); // Corvax-Secrets
    /// <summary>
    /// Be advised this can be called from server packaging during a HybridACZ build.
    /// </summary>
    public static async Task PackageClient(bool skipBuild, string configuration, IPackageLogger logger)
    {
        logger.Info("Building client...");

        if (!skipBuild)
        {
            await ProcessHelpers.RunCheck(new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "build",
                    Path.Combine("Content.Client", "Content.Client.csproj"),
                    "-c", configuration,
                    "--nologo",
                    "/v:m",
                    "/t:Rebuild",
                    "/p:FullRelease=true",
                    "/m"
                }
            });
            if (UseSecrets)
            {
                await ProcessHelpers.RunCheck(new ProcessStartInfo
                {
                    FileName = "dotnet",
                    ArgumentList =
                    {
                        "build",
                        Path.Combine("Secrets","Content.Corvax.Client", "Content.Corvax.Client.csproj"),
                        "-c", "Release",
                        "--nologo",
                        "/v:m",
                        "/t:Rebuild",
                        "/p:FullRelease=true",
                        "/m"
                    }
                });
            }
        }

        logger.Info("Packaging client...");

        var sw = RStopwatch.StartNew();
        {
            await using var zipFile =
                File.Open(Path.Combine("release", "SS14.Client.zip"), FileMode.Create, FileAccess.ReadWrite);
            using var zip = new ZipArchive(zipFile, ZipArchiveMode.Update);
            var writer = new AssetPassZipWriter(zip);

            await WriteResources("", writer, logger, default);
            await writer.FinishedTask;
        }

        logger.Info($"Finished packaging client in {sw.Elapsed}");
    }

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

        // Corvax-Secrets-Start: Add Corvax interfaces to Magic ACZ
        var assemblies = new List<string> { "Content.Client", "Content.Shared", "Content.Shared.Database", "Content.Corvax.Interfaces.Client", "Content.Corvax.Interfaces.Shared" };
        if (UseSecrets)
            assemblies.AddRange(new[] { "Content.Corvax.Shared", "Content.Corvax.Client" });
        // Corvax-Secrets-End

        await RobustSharedPackaging.WriteContentAssemblies(
            inputPass,
            contentDir,
            "Content.Client",
            assemblies, // Corvax-Secrets
            cancel: cancel);

        await WriteClientResources(contentDir, pass, cancel); // Corvax-Secrets: Support content resource ignore to ignore server-only prototypes

        inputPass.InjectFinished();
    }

    // Corvax-Secrets-Start
    public static IReadOnlySet<string> ContentClientIgnoredResources { get; } = new HashSet<string>
    {
        "CorvaxSecretsServer"
    };

    private static async Task WriteClientResources(
        string contentDir,
        AssetPass pass,
        CancellationToken cancel = default)
    {
        var ignoreSet = RobustClientPackaging.ClientIgnoredResources
            .Union(RobustSharedPackaging.SharedIgnoredResources)
            .Union(ContentClientIgnoredResources).ToHashSet();

        await RobustSharedPackaging.DoResourceCopy(Path.Combine(contentDir, "Resources"), pass, ignoreSet, cancel: cancel);
    }
    // Corvax-Secrets-End
}

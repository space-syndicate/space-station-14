// ReSharper disable UseCollectionExpression
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests;
using Content.Shared.CCVar;
using Robust.Shared.Utility;

namespace Content.Corvax.Tests.Syndicate;

/// <summary>
/// Runs PostMapInit tests only for maps from /Maps/Corvax/**.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class PostMapInitCorvaxTest : PostMapInitTestBase
{
    private const string IncludedCorvax = "/Maps/Corvax";

    [Test, TestCaseSource(nameof(GridsSource), new object[] { true, new[] { IncludedCorvax } })]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task GridsLoadableTest(string mapFile)
    {
        await RunGridsLoadableTest(mapFile);
    }

    [Test, TestCaseSource(nameof(ShuttleMapFilesSource), new object[] { true, new[] { IncludedCorvax } })]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task ShuttlesLoadableTest(ResPath path)
    {
        await RunShuttlesLoadableTest(path);
    }

    [Test, TestCaseSource(nameof(AllMapFilesSource), new object[] { true, new[] { IncludedCorvax } })]
    public async Task NoSavedPostMapInitTest(ResPath map)
    {
        await RunNoSavedPostMapInitTest(map);
    }

    [Test, TestCaseSource(nameof(GameMapsSource))]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task GameMapsLoadableTest(string mapProto)
    {
        await RunGameMapsLoadableTest(mapProto, customOnly: true, new[] { IncludedCorvax });
    }

    [Test, TestCaseSource(nameof(AllMapFilesSource), new object[] { true, new[] { IncludedCorvax } })]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task NonGameMapsLoadableTest(ResPath mapPath)
    {
        await RunNonGameMapsLoadableTest(mapPath);
    }
}

using System.Linq;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Corvax.GuideGenerator;
using Content.Client.Gameplay;
using Robust.Client;
using Robust.Client.State;
using Robust.Client.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.ExportSprites;

public sealed class EntityScreenshotGenerator
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IBaseClient _baseClient = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly EntityScreenshotRenderService _renderService = default!;
    [Dependency] private readonly IGameController _gameController = default!;
    [Dependency] private readonly IClientGameTiming _gameTiming = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    private ISawmill _sawmill = default!;
    private bool _started;
    private bool _startupRequested;
    private bool _pendingStart;
    private const uint WarmupFrames = 3;

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _logManager.GetSawmill("entity-screenshot-generator");
        _renderService.Initialize();
    }

    public bool PostInit()
    {
        if (!_cfg.GetCVar(CCVars.EntityScreenshotGeneratorEnabled))
            return false;

        if (_baseClient.RunLevel == ClientRunLevel.SinglePlayerGame)
        {
            _pendingStart = true;
            return true;
        }

        if (_startupRequested)
            return true;

        _startupRequested = true;
        _baseClient.StartSinglePlayer();
        _stateManager.RequestStateChange<GameplayState>();
        _pendingStart = true;
        return true;
    }

    public void Update()
    {
        if (!_pendingStart || _started)
            return;

        if (_baseClient.RunLevel != ClientRunLevel.SinglePlayerGame)
            return;

        TryStart();
    }

    public bool TryStart()
    {
        if (_started || !_cfg.GetCVar(CCVars.EntityScreenshotGeneratorEnabled))
            return _started;

        try
        {
            _entitySystemManager.GetEntitySystem<SharedMapSystem>();
        }
        catch
        {
            return false;
        }

        try
        {
            _ = RunAsync();
        }
        catch (Exception)
        {
            return false;
        }

        _started = true;
        _pendingStart = false;
        return true;
    }

    private async Task RunAsync()
    {
        var outputDir = new ResPath(_cfg.GetCVar(CCVars.EntityScreenshotOutputPath));
        var wasPaused = _gameTiming.Paused;

        try
        {
            _gameTiming.Paused = true;

            var failures = new List<string>();
            var exported = 0;
            var mapSystem = _entitySystemManager.GetEntitySystem<SharedMapSystem>();
            var allowedIds = EntityProjectHelper.GetProjectEntityIds();
            var prototypes = _prototypeManager.EnumeratePrototypes<EntityPrototype>()
                .Where(proto =>
                    !proto.Abstract &&
                    proto.Components.ContainsKey("Sprite") &&
                    EntityProjectHelper.MatchesAllowedIds(proto.ID, allowedIds))
                .OrderBy(proto => proto.ID)
                .ToList();
            var previewMap = mapSystem.CreateMap(out var mapId);
            var previewGrid = _mapManager.CreateGridEntity(mapId);

            if (!_resourceManager.UserData.IsDir(outputDir))
                _resourceManager.UserData.CreateDir(outputDir);

            foreach (var proto in prototypes)
            {
                EntityUid entity = default;

                try
                {
                    entity = _entityManager.SpawnEntity(proto.ID, new EntityCoordinates(previewGrid.Owner, default));

                    await WaitForEntityAppearanceAsync(entity);
                    ApplyPrototypeAppearance(entity, proto);
                    await WaitForEntityAppearanceAsync(entity, 1);

                    await _renderService.Export(entity, Direction.South, outputDir / $"{proto.ID}.png");
                    exported++;
                }
                catch (Exception e)
                {
                    failures.Add($"{proto.ID}: {e.Message}");
                    _sawmill.Error($"Failed to export {proto.ID}: {e}");
                }
                finally
                {
                    if (_entityManager.EntityExists(entity))
                        _entityManager.DeleteEntity(entity);
                }
            }

            if (failures.Count > 0)
                WriteFailures(outputDir, failures);

            if (_entityManager.EntityExists(previewGrid))
                _entityManager.DeleteEntity(previewGrid);

            if (_entityManager.EntityExists(previewMap))
                _entityManager.DeleteEntity(previewMap);

            _gameController.Shutdown($"Entity screenshot generation complete. Exported {exported}/{prototypes.Count}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Entity screenshot generation crashed: {e}");
            WriteFailures(outputDir, new[] { e.ToString() });
            _gameController.Shutdown("Entity screenshot generation failed");
        }
        finally
        {
            _gameTiming.Paused = wasPaused;
        }
    }

    private void WriteFailures(ResPath outputDir, IEnumerable<string> failures)
    {
        if (!_resourceManager.UserData.IsDir(outputDir))
            _resourceManager.UserData.CreateDir(outputDir);

        using var writer = _resourceManager.UserData.OpenWriteText(outputDir / "failures.txt");
        foreach (var failure in failures)
        {
            writer.WriteLine(failure);
        }

        writer.Flush();
    }

    private async Task WaitForEntityAppearanceAsync(EntityUid entity)
    {
        await WaitForEntityAppearanceAsync(entity, WarmupFrames);
    }

    private async Task WaitForEntityAppearanceAsync(EntityUid entity, uint frames)
    {
        if (!_entityManager.TryGetComponent(entity, out MetaDataComponent? metadata))
            return;

        if (!metadata.EntityInitialized)
            _entityManager.InitializeAndStartEntity((entity, metadata), doMapInit: true);

        var targetFrame = _gameTiming.CurFrame + frames;

        while (_entityManager.EntityExists(entity) && _gameTiming.CurFrame < targetFrame)
        {
            await Task.Delay(1);
        }
    }

    private void ApplyPrototypeAppearance(EntityUid entity, EntityPrototype prototype)
    {
        if (!_entityManager.TryGetComponent(entity, out AppearanceComponent? appearance))
            return;

        if (!prototype.TryGetComponent<SolutionContainerManagerComponent>(out var manager, _entityManager.ComponentFactory) ||
            manager.Solutions == null ||
            manager.Solutions.Count == 0)
        {
            return;
        }

        var solutionEntry = manager.Solutions.FirstOrDefault(entry => entry.Value.Volume > 0);
        if (string.IsNullOrEmpty(solutionEntry.Key))
            solutionEntry = manager.Solutions.First();

        var solution = solutionEntry.Value;
        var appearanceSystem = _entitySystemManager.GetEntitySystem<SharedAppearanceSystem>();

        appearanceSystem.SetData(entity, SolutionContainerVisuals.FillFraction, solution.FillFraction, appearance);
        appearanceSystem.SetData(entity, SolutionContainerVisuals.Color, solution.GetColor(_prototypeManager), appearance);
        appearanceSystem.SetData(entity, SolutionContainerVisuals.SolutionName, solutionEntry.Key, appearance);

        if (solution.GetPrimaryReagentId() is { } reagent)
            appearanceSystem.SetData(entity, SolutionContainerVisuals.BaseOverride, reagent.ToString(), appearance);
    }
}

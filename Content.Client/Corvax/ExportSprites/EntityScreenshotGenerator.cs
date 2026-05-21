using System.Linq;
using System.Threading.Tasks;
using Content.Client.Gameplay;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Corvax.GuideGenerator;
using Content.Shared.Prototypes;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.State;
using Robust.Client.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
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
    [Dependency] private readonly ISerializationManager _serialization = default!;
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
                    HasExportableSprite(proto) &&
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
                    if (proto.HasComponent<SpriteComponent>(_entityManager.ComponentFactory))
                    {
                        entity = _entityManager.SpawnEntity(proto.ID, new EntityCoordinates(previewGrid.Owner, default));

                        await WaitForEntityAppearanceAsync(entity);
                        ApplyPrototypeAppearance(entity, proto);
                        await WaitForEntityAppearanceAsync(entity, 1);
                    }
                    else
                    {
                        entity = SpawnIconEntity(proto);
                    }

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

    private bool HasExportableSprite(EntityPrototype prototype)
    {
        if (prototype.HasComponent<SpriteComponent>(_entityManager.ComponentFactory))
            return true;

        return TryGetPrototypeIcon(prototype, out _);
    }

    private EntityUid SpawnIconEntity(EntityPrototype prototype)
    {
        if (!TryGetPrototypeIcon(prototype, out var icon) || icon == null)
            throw new InvalidOperationException($"Prototype {prototype.ID} has no exportable icon.");

        var entity = _entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
        var sprite = _entityManager.EnsureComponent<SpriteComponent>(entity);
        var spriteSystem = _entitySystemManager.GetEntitySystem<SpriteSystem>();

        spriteSystem.AddBlankLayer((entity, sprite), 0);
        if (icon is SpriteSpecifier.EntityPrototype entityIcon)
            spriteSystem.LayerSetTexture((entity, sprite), 0, spriteSystem.Frame0(new SpriteSpecifier.EntityPrototype(entityIcon.EntityPrototypeId)));
        else
            spriteSystem.LayerSetSprite((entity, sprite), 0, icon);
        sprite.LayerSetShader(0, "unshaded");
        spriteSystem.LayerSetVisible((entity, sprite), 0, true);

        return entity;
    }

    private bool TryGetPrototypeIcon(EntityPrototype prototype, out SpriteSpecifier? icon)
    {
        icon = null;

        foreach (var (_, entry) in prototype.Components)
        {
            if (TryExtractSpriteSpecifier(entry.Component.GetType(), entry.Mapping, out icon))
                return true;
        }

        return false;
    }

    private bool TryExtractSpriteSpecifier(Type? expectedType, DataNode? node, out SpriteSpecifier? icon)
    {
        icon = null;

        if (node == null)
            return false;

        if (expectedType != null &&
            typeof(SpriteSpecifier).IsAssignableFrom(expectedType) &&
            TryParseSpriteSpecifier(node, out icon))
        {
            return true;
        }

        if (node is MappingDataNode mapping)
        {
            foreach (var (key, child) in mapping.Children)
            {
                Type? childType = null;

                if (expectedType != null &&
                    _serialization.TryGetVariableType(expectedType, key, out var resolvedType))
                {
                    childType = resolvedType;
                }

                if (TryExtractSpriteSpecifier(childType, child, out icon))
                    return true;
            }

            return false;
        }

        if (node is SequenceDataNode sequence)
        {
            var elementType = GetSequenceElementType(expectedType);
            foreach (var child in sequence.Sequence)
            {
                if (TryExtractSpriteSpecifier(elementType, child, out icon))
                    return true;
            }
        }

        return false;
    }

    private static Type? GetSequenceElementType(Type? type)
    {
        if (type == null)
            return null;

        if (type.IsArray)
            return type.GetElementType();

        var genericArguments = type.GenericTypeArguments;
        if (genericArguments.Length == 1)
            return genericArguments[0];

        return null;
    }

    private bool TryParseSpriteSpecifier(DataNode node, out SpriteSpecifier? icon)
    {
        icon = null;

        try
        {
            icon = _serialization.Read<SpriteSpecifier>(node, notNullableOverride: true);
            if (icon == SpriteSpecifier.Invalid)
            {
                icon = null;
                return false;
            }

            return true;
        }
        catch
        {
            icon = null;
            return false;
        }
    }
}

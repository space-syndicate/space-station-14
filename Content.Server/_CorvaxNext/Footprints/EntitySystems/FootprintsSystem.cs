using Content.Server.Atmos.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Shared._CorvaxNext.Footprints;
using Content.Shared._CorvaxNext.Footprints.Components;
using Content.Shared._CorvaxNext.Standing;

namespace Content.Server._CorvaxNext.Footprints.EntitySystems;

public sealed class FootprintsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IMapManager _map = default!;

    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<MobThresholdsComponent> _mobThresholdQuery;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<LayingDownComponent> _layingQuery;

    public override void Initialize()
    {
        _transformQuery = GetEntityQuery<TransformComponent>();
        _mobThresholdQuery = GetEntityQuery<MobThresholdsComponent>();
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _layingQuery = GetEntityQuery<LayingDownComponent>();

        SubscribeLocalEvent<FootprintVisualizerComponent, ComponentStartup>(OnStartupComponent);
        SubscribeLocalEvent<FootprintVisualizerComponent, MoveEvent>(OnMove);
    }

    private void OnStartupComponent(EntityUid uid, FootprintVisualizerComponent component, ComponentStartup args)
    {
        component.StepSize = Math.Max(0f, component.StepSize + _random.NextFloat(-0.05f, 0.05f));
    }

    private void OnMove(EntityUid uid, FootprintVisualizerComponent component, ref MoveEvent args)
    {
        if (component.PrintsColor.A <= 0f)
            return;

        if (!_transformQuery.TryComp(uid, out var transform))
            return;

        if (!_mobThresholdQuery.TryComp(uid, out var mobThreshHolds))
            return;

        if (!_map.TryFindGridAt(_transform.GetMapCoordinates((uid, transform)), out var gridUid, out _))
            return;

        var dragging = mobThreshHolds.CurrentThresholdState is MobState.Critical or MobState.Dead || _layingQuery.TryComp(uid, out var laying) && laying.DrawDowned;
        var distance = (transform.LocalPosition - component.StepPos).Length();
        var stepSize = dragging ? component.DragSize : component.StepSize;

        if (distance <= stepSize)
            return;

        component.RightStep = !component.RightStep;

        var entity = Spawn(component.StepProtoId, CalcCoords(gridUid, component, transform, dragging));
        var footPrintComponent = EnsureComp<FootprintComponent>(entity);

        footPrintComponent.FootprintsVisualizer = uid;
        Dirty(entity, footPrintComponent);

        if (_appearanceQuery.TryComp(entity, out var appearance))
        {
            _appearance.SetData(entity, FootprintVisualState.State, PickState(uid, dragging), appearance);
            _appearance.SetData(entity, FootprintVisualState.Color, component.PrintsColor, appearance);
        }

        if (!_transformQuery.TryComp(entity, out var stepTransform))
            return;

        stepTransform.LocalRotation = dragging
            ? (transform.LocalPosition - component.StepPos).ToAngle() + Angle.FromDegrees(-90f)
            : transform.LocalRotation + Angle.FromDegrees(180f);

        component.PrintsColor = component.PrintsColor.WithAlpha(Math.Max(0f, component.PrintsColor.A - component.ColorReduceAlpha));
        component.StepPos = transform.LocalPosition;

        if (!TryComp<SolutionContainerManagerComponent>(entity, out var solutionContainer))
            return;

        if (!_solution.ResolveSolution((entity, solutionContainer), footPrintComponent.SolutionName, ref footPrintComponent.Solution, out var solution))
            return;

        if (string.IsNullOrWhiteSpace(component.ReagentToTransfer) || solution.Volume >= 1)
            return;

        _solution.TryAddReagent(footPrintComponent.Solution.Value, component.ReagentToTransfer, 0.5, out _);
    }

    private EntityCoordinates CalcCoords(EntityUid uid, FootprintVisualizerComponent component, TransformComponent transform, bool state)
    {
        if (state)
            return new EntityCoordinates(uid, transform.LocalPosition);

        var offset = component.RightStep
            ? new Angle(Angle.FromDegrees(180f) + transform.LocalRotation).RotateVec(component.OffsetPrint)
            : new Angle(transform.LocalRotation).RotateVec(component.OffsetPrint);

        return new EntityCoordinates(uid, transform.LocalPosition + offset);
    }

    private FootprintVisuals PickState(EntityUid uid, bool dragging)
    {
        var state = FootprintVisuals.BareFootprint;

        if (dragging)
            state = FootprintVisuals.Dragging;
        else if (_inventory.TryGetSlotEntity(uid, "outerClothing", out var suit) && TryComp<PressureProtectionComponent>(suit, out _))
            state = FootprintVisuals.SuitPrint;
        else if (_inventory.TryGetSlotEntity(uid, "shoes", out _))
            state = FootprintVisuals.ShoesPrint;

        return state;
    }
}

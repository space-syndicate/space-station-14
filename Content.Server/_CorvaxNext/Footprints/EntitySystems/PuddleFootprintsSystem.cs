using System.Linq;
using Content.Shared._CorvaxNext.Footprints.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server._CorvaxNext.Footprints.EntitySystems;

public sealed class PuddleFootprintsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PuddleFootprintsComponent, EndCollideEvent>(OnStepTrigger);
    }

    private void OnStepTrigger(EntityUid uid, PuddleFootprintsComponent component, ref EndCollideEvent args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        if (!TryComp<PuddleComponent>(uid, out var puddle))
            return;

        if (!TryComp<FootprintVisualizerComponent>(args.OtherEntity, out var tripper))
            return;

        if (!TryComp<SolutionContainerManagerComponent>(uid, out var solutionManager))
            return;

        if (!_solutionContainer.ResolveSolution((uid, solutionManager), puddle.SolutionName, ref puddle.Solution, out var solutions))
            return;

        var totalSolutionQuantity = solutions.Contents.Sum(sol => (float)sol.Quantity);
        var waterQuantity = (float)solutions.Contents.Where(solution => solution.Reagent.Prototype == "Water").FirstOrDefault().Quantity;

        if (waterQuantity / (totalSolutionQuantity / 100f) > component.OffPercent || solutions.Contents.Count <= 0)
            return;

        tripper.ReagentToTransfer = solutions.Contents.Aggregate((l, r) => l.Quantity > r.Quantity ? l : r).Reagent.Prototype;

        if (_appearance.TryGetData(uid, PuddleVisuals.SolutionColor, out var color, appearance)
            && _appearance.TryGetData(uid, PuddleVisuals.CurrentVolume, out var volume, appearance))
            AddColor((Color)color, (float)volume * component.SizeRatio, tripper);

        _solutionContainer.RemoveEachReagent(puddle.Solution.Value, 1);
    }

    private void AddColor(Color col, float quantity, FootprintVisualizerComponent component)
    {
        component.PrintsColor = component.ColorQuantity == 0 ? col : Color.InterpolateBetween(component.PrintsColor, col, component.ColorInterpolationFactor);
        component.ColorQuantity += quantity;
    }
}

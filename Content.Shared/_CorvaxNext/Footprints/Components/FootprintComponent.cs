using Content.Shared.Chemistry.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxNext.Footprints.Components;

/// <summary>
/// This is used for marking footsteps, handling footprint drawing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FootprintComponent : Component
{
    /// <summary>
    /// Owner (with <see cref="FootprintVisualizerComponent"/>) of a print (this component).
    /// </summary>
    [AutoNetworkedField]
    public EntityUid FootprintsVisualizer;

    [DataField]
    public string SolutionName = "step";

    [DataField]
    public Entity<SolutionComponent>? Solution;
}

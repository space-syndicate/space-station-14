using Robust.Shared.GameStates;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Added to mind role entities to tag that they are a Malfunction AI antagonist.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MalfunctionAiRoleComponent : BaseMindRoleComponent;

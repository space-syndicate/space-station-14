using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.TableSlam;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TableableComponent : Component
{
    /// <summary>
    /// If this pullable being tabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BeingTabled = false;

    /// <summary>
    /// Constant for tabling throw math
    /// </summary>
    [DataField]
    public float BasedTabledForceSpeed = 5f;

    /// <summary>
    ///  Stamina damage. taken on tabled
    /// </summary>
    [DataField]
    public float TabledStaminaDamage = 40f;

    /// <summary>
    /// Damage taken on being tabled.
    /// </summary>
    [DataField]
    public float TabledDamage = 5f;
    // Goobstation end
}
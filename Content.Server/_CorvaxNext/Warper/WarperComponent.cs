namespace Content.Server._CorvaxNext.Warper;

[RegisterComponent]
public sealed partial class WarperComponent : Component
{
    /// Warp destination unique identifier.
    [ViewVariables(VVAccess.ReadWrite)] [DataField("id")] public string? ID { get; set; }
}

namespace Content.Shared.Backmen.StationAI.Components;

[RegisterComponent]
public sealed partial class BrokenAiComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)] public readonly string BrokenAiSyndicateChannel = "Syndicate";
}

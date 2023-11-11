using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.StationAI;

[RegisterComponent]
public sealed partial class InnateItemComponent : Component
{
    public bool AlreadyInitialized = false;

    [DataField("afterInteract")]
    public bool AfterInteract = true;

    [DataField("startingPriority")]
    public int? StartingPriority = null;

    [DataField("slots")]
    public Dictionary<string, EntProtoId> Slots = new Dictionary<string, EntProtoId>();

    public Dictionary<string, EntityUid> Actions = new Dictionary<string, EntityUid>();
}

using Content.Shared.Actions;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Corvax.Ipc;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class IpcComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> BatteryAlert = "BorgBattery";

    [DataField]
    public ProtoId<AlertPrototype> NoBatteryAlert = "BorgBatteryNone";

    [DataField]
    public EntProtoId DrainBatteryAction = "ActionDrainBattery";

    [DataField]
    public EntProtoId ChangeFaceAction = "ActionIpcChangeFace";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField]
    public EntityUid? ChangeFaceActionEntity;

    [DataField, AutoNetworkedField]
    public string SelectedFace = string.Empty;

    [DataField, AutoNetworkedField]
    public ProtoId<IpcFaceProfilePrototype> FaceProfile = "DefaultIpcFaces";

    public bool DrainActivated;
}

public sealed partial class ToggleDrainActionEvent : InstantActionEvent
{

}

public sealed partial class OpenIpcFaceActionEvent : InstantActionEvent
{
}

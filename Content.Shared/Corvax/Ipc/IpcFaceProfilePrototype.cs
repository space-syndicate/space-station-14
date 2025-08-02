using Robust.Shared.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Corvax.Ipc;

/// <summary>
/// Prototype defining a collection of IPC face sprites.
/// </summary>
[Prototype("ipcFaceProfile")]
public sealed partial class IpcFaceProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Available face markings for this profile.
    /// </summary>
    [DataField("faces", required: true)]
    public List<ProtoId<MarkingPrototype>> Faces { get; private set; } = new();
}

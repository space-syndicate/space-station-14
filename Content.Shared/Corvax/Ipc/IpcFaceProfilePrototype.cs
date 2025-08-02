using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

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
    /// Path to the RSI containing all IPC face states.
    /// </summary>
    [DataField("rsi")]
    public string RsiPath { get; private set; } = string.Empty;
}

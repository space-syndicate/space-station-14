using Content.Server.Paper;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DocumentCopier;

[RegisterComponent]
public sealed class DocumentCopierComponent : Component
{
    /// <summary>
    /// Contains the paper to copy document to it
    /// </summary>
    [DataField("targetSheet", required: false)]
    public ItemSlot TargetSheet = new();

    /// <summary>
    /// Contains the document to copy to blank sheet
    /// </summary>
    [DataField("sourceSheet", required: false)]
    public ItemSlot SourceSheet = new();

    /// <summary>
    /// Sound to play when copier printing a copy
    /// </summary>
    [DataField("printSound", required: false)]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>
    /// Print queue of the incoming message
    /// </summary>
    [ViewVariables]
    [DataField("printingQueue")]
    public Queue<DocumentCopierPrintout> PrintingQueue { get; } = new();

    /// <summary>
    /// Remaining time of inserting animation
    /// </summary>
    [DataField("insertingTimeRemaining")]
    public float InsertingTimeRemaining;

    /// <summary>
    /// How long the inserting animation will play
    /// </summary>
    [ViewVariables]
    public float InsertionTime = 1.88f; // 0.02 off for correct animation

    /// <summary>
    /// Remaining time of printing animation
    /// </summary>
    [DataField("printingTimeRemaining")]
    public float PrintingTimeRemaining;

    /// <summary>
    /// How long the printing animation will play
    /// </summary>
    [ViewVariables]
    public float PrintingTime = 2.3f;
}

public sealed class DocumentCopierPrintout
{
    [DataField("name", required: true)]
    public string Name { get; }

    [DataField("content", required: true)]
    public string Content { get; }

    [DataField("prototypeId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>), required: true)]
    public string PrototypeId { get; set;  }

    [DataField("stampState")]
    public string? StampState { get; set; }

    [DataField("stampedBy")]
    public List<string> StampedBy { get; }

    public DocumentCopierPrintout(string content, string name, string? prototypeId = null, string? stampState = null, List<string>? stampedBy = null)
    {
        Content = content;
        Name = name;
        PrototypeId = prototypeId ?? "";
        StampState = stampState;
        StampedBy = stampedBy ?? new List<string>();
    }
}

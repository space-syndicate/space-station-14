using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;

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

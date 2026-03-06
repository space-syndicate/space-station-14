using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.GameTicking.Prototypes;

/// <summary>
/// Prototype for a lobby background the game can choose.
/// </summary>
[Prototype]
public sealed partial class LobbyBackgroundPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// The sprite to use as the background. This should ideally be 1920x1080.
    /// </summary>
    [DataField]
    public ResPath? Background;

    /// <summary>
    /// The video to use as the background. If set, this will play instead of the static image.
    /// Supports webm and mp4 formats. Video should ideally be 1920x1080.
    /// </summary>
    [DataField]
    public ResPath? Video;

    /// <summary>
    /// Whether the video should loop when it finishes.
    /// </summary>
    [DataField]
    public bool VideoLoop = true;

    /// <summary>
    /// The volume of the video audio (0.0 to 1.0).
    /// </summary>
    [DataField]
    public float VideoVolume = 0.5f;

    /// <summary>
    /// The title of the background to be displayed in the lobby.
    /// </summary>
    [DataField]
    public LocId Title = "lobby-state-background-unknown-title";

    /// <summary>
    /// The artist who made the art for the background.
    /// </summary>
    [DataField]
    public LocId Artist = "lobby-state-background-unknown-artist";
}

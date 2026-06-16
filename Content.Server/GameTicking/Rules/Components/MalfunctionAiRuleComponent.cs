using Content.Server.GameTicking.Rules;
using Content.Shared.Silicons.Laws;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// Game rule that turns the station's AI into a "Malfunction AI" antagonist.
/// The AI keeps its existing core entity but receives new laws, malf abilities and objectives.
/// </summary>
[RegisterComponent, Access(typeof(MalfunctionAiRuleSystem))]
public sealed partial class MalfunctionAiRuleComponent : Component
{
    /// <summary>
    /// Minds that have been turned into a Malfunction AI by this rule.
    /// </summary>
    public readonly List<EntityUid> MalfMinds = new();

    /// <summary>
    /// The lawset granted to the AI when it becomes malfunctioning.
    /// </summary>
    [DataField]
    public ProtoId<SiliconLawsetPrototype> Lawset = "Malfunction";

    /// <summary>
    /// Sound played to the AI player when they are made into a Malfunction AI.
    /// </summary>
    [DataField]
    public SoundSpecifier? GreetSound = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");

    // --- Doomsday device ---

    /// <summary>
    /// AI entity that armed the Doomsday device; the explosion is centered here.
    /// </summary>
    [ViewVariables]
    public EntityUid? DoomsdayAi;

    /// <summary>
    /// True once the Doomsday countdown has started.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool DoomsdayArmed;

    /// <summary>
    /// True once the Doomsday explosion has actually fired (win condition).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool DoomsdayDetonated;

    /// <summary>
    /// Seconds left on the Doomsday timer.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DoomsdayRemaining = 270f;

    /// <summary>
    /// Announcement thresholds (in seconds remaining) that have not yet been broadcast.
    /// </summary>
    [DataField]
    public List<int> DoomsdayAnnouncementsLeft = new() { 240, 180, 120, 60, 30, 10 };

    /// <summary>
    /// Total intensity for the Doomsday explosion.
    /// </summary>
    [DataField]
    public float DoomsdayExplosionIntensity = 150000f;

    /// <summary>
    /// Per-tile intensity cap for the Doomsday explosion.
    /// </summary>
    [DataField]
    public float DoomsdayMaxTileIntensity = 100f;

    /// <summary>
    /// Slope (falloff rate) of the Doomsday explosion.
    /// </summary>
    [DataField]
    public float DoomsdayExplosionSlope = 5f;

    /// <summary>
    /// Type of the Doomsday explosion.
    /// </summary>
    [DataField]
    public string DoomsdayExplosionType = "Default";
}

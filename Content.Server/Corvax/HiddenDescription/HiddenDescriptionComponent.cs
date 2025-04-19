using Content.Server.Corvax.HiddenDescription.Prototypes;
using Content.Shared.Localizations;
using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Maths;

namespace Content.Server.Corvax.HiddenDescription;

/// <summary>
/// Component allowing entities to display additional information during examination,
/// based on the examiner's roles, held status, or other conditions.
/// </summary>
[RegisterComponent, Access(typeof(HiddenDescriptionSystem))]
public sealed partial class HiddenDescriptionComponent : Component
{
    /// <summary>
    /// A list of entries, each defining a piece of hidden text and the criteria to view it.
    /// </summary>
    [DataField("entries", required: true)]
    public List<HiddenDescriptionEntry> Entries = new();

    /// <summary>
    /// Priority for displaying this component's descriptions relative to other examine text.
    /// </summary>
    [DataField("priority")]
    public int PushPriority = 1;
}

/// <summary>
/// Defines a single hidden description and the requirements to see it.
/// </summary>
[DataDefinition, Serializable]
public readonly partial record struct HiddenDescriptionEntry()
{
    /// <summary>
    /// Localization ID for the description text. Used if <see cref="RawText"/> is null/empty.
    /// </summary>
    [DataField("label")]
    public LocId Label { get; init; } = default!;

    /// <summary>
    /// Raw text for the description. Overrides <see cref="Label"/> if set. Bypasses localization.
    /// </summary>
    [DataField("rawText")]
    public string? RawText { get; init; }

    /// <summary>
    /// Optional color for the description text.
    /// </summary>
    [DataField("color")]
    public Color? TextColor { get; init; }

    /// <summary>
    /// Whitelist rules that the examiner's mind entity must pass.
    /// </summary>
    [DataField("whitelistMind")]
    public EntityWhitelist WhitelistMind { get; init; } = new();

    /// <summary>
    /// Whitelist rules that the examiner's body entity must pass.
    /// </summary>
    [DataField("whitelistBody")]
    public EntityWhitelist WhitelistBody { get; init; } = new();

    /// <summary>
    /// ID of a <see cref="HiddenDescriptionRoleGroupPrototype"/> required.
    /// The examiner must have a job listed in this group.
    /// </summary>
    [DataField("roleGroupRequired")]
    public ProtoId<HiddenDescriptionRoleGroupPrototype>? RoleGroupRequired { get; init; }

    /// <summary>
    /// If true, the examiner must be holding the examined entity.
    /// </summary>
    [DataField("mustBeHeld")]
    public bool MustBeHeld { get; init; } = false;

    /// <summary>
    /// If true, ALL role/whitelist checks must pass. If false, ANY check is sufficient.
    /// Note: <see cref="MustBeHeld"/> is always checked if set to true.
    /// </summary>
    [DataField("requireAll")]
    public bool NeedAllCheck { get; init; } = false;
}

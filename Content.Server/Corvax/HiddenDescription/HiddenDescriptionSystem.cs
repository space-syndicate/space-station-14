using Content.Shared.Corvax.HiddenDescription.Prototypes;
using Content.Server.Mind;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Content.Shared.Mind.Components;
using Robust.Shared.Maths;

namespace Content.Server.Corvax.HiddenDescription;

/// <summary>
/// Handles examining entities with <see cref="HiddenDescriptionComponent"/>.
/// Checks examiner roles/whitelists/held status against entries and pushes descriptions.
/// Supports localized ('label'), raw ('rawText'), and colored ('color') descriptions.
/// </summary>
public sealed partial class HiddenDescriptionSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HiddenDescriptionComponent, ExaminedEvent>(OnExamine);
    }

    /// <summary>
    /// Main handler for the ExaminedEvent. Orchestrates the checks.
    /// </summary>
    private void OnExamine(Entity<HiddenDescriptionComponent> entity, ref ExaminedEvent args)
    {
        var (examinedUid, component) = entity;
        var examiner = args.Examiner;

        bool mindFound = _mind.TryGetMind(examiner, out var mindId, out _);
        ProtoId<JobPrototype>? playerJobProtoId = GetPlayerJobProtoId(mindFound, mindId);

        foreach (var entry in component.Entries)
        {
            bool roleWhitelistCheckPassed = CheckRoleWhitelistRequirements(entry, mindFound, mindId, examiner, playerJobProtoId, examinedUid);
            bool holdingCheckPassed = CheckHoldingRequirement(entry, examiner, examinedUid);

            bool passed = roleWhitelistCheckPassed && holdingCheckPassed;

            if (passed)
            {
                if (TryGetDescriptionText(entry, examinedUid, out var textToShow))
                {
                    textToShow = ApplyColor(textToShow, entry.TextColor);
                    args.PushMarkup(textToShow, component.PushPriority);
                }
            }
        }
    }

    // --- Helper Methods ---

    /// <summary>
    /// Attempts to get the Job Prototype ID for the player's mind.
    /// </summary>
    private ProtoId<JobPrototype>? GetPlayerJobProtoId(bool mindFound, EntityUid? mindId)
    {
        if (!mindFound || mindId == null)
            return null;

        var hasJobRole = _roles.MindHasRole<JobRoleComponent>(mindId.Value, out var role);
        return hasJobRole && role?.Comp1 != null ? role.Value.Comp1.JobPrototype : null;
    }

    /// <summary>
    /// Checks if the role and whitelist requirements for an entry are met.
    /// </summary>
    private bool CheckRoleWhitelistRequirements(
        HiddenDescriptionEntry entry,
        bool mindFound,
        EntityUid? mindId,
        EntityUid examiner,
        ProtoId<JobPrototype>? playerJobProtoId,
        EntityUid examinedUid)
    {
        var isJobAllowed = false;
        if (entry.RoleGroupRequired is { } requiredGroupProtoId && playerJobProtoId is { } jobProtoId)
        {
            if (_prototypeManager.TryIndex(requiredGroupProtoId, out var roleGroupProto))
            {
                isJobAllowed = roleGroupProto.Jobs.Contains(jobProtoId);
            }
            else
            {
                Log.Warning($"{nameof(HiddenDescriptionComponent)} on entity {ToPrettyString(examinedUid)} references non-existent {nameof(HiddenDescriptionRoleGroupPrototype)}: {requiredGroupProtoId}");
            }
        }

        bool isMindWhitelistPassed = mindFound && mindId.HasValue && _whitelist.IsValid(entry.WhitelistMind, mindId.Value);
        bool isBodyWhitelistPassed = _whitelist.IsValid(entry.WhitelistBody, examiner);

        return entry.NeedAllCheck
            ? (isMindWhitelistPassed && isBodyWhitelistPassed && (entry.RoleGroupRequired == null || isJobAllowed))
            : (isMindWhitelistPassed || isBodyWhitelistPassed || isJobAllowed);
    }

    /// <summary>
    /// Checks if the MustBeHeld requirement is met.
    /// </summary>
    private bool CheckHoldingRequirement(HiddenDescriptionEntry entry, EntityUid examiner, EntityUid examinedUid)
    {
        return !entry.MustBeHeld || _handsSystem.IsHolding(examiner, examinedUid, out _);
    }

    /// <summary>
    /// Tries to get the appropriate description text (prioritizing rawText).
    /// </summary>
    private bool TryGetDescriptionText(HiddenDescriptionEntry entry, EntityUid examinedUid, out string text)
    {
        if (!string.IsNullOrWhiteSpace(entry.RawText))
        {
            text = entry.RawText;
            return true;
        }

        if (entry.Label != default)
        {
            text = Loc.GetString(entry.Label);
            return true;
        }

        Log.Warning($"HiddenDescriptionEntry on {ToPrettyString(examinedUid)} for role group '{entry.RoleGroupRequired?.ToString() ?? "N/A"}' is missing both 'rawText' and a valid 'label'.");
        text = string.Empty;
        return false;
    }

    /// <summary>
    /// Applies the specified color to the text using markup tags.
    /// </summary>
    private string ApplyColor(string text, Color? color)
    {
        if (color != null)
        {
            if (!text.StartsWith("[color="))
                text = $"[color={color.Value.ToHex()}]{text}[/color]";
        }
        return text;
    }
}

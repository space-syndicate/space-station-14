using Content.Server.GameTicking;
using Content.Shared.Administration.Logs;
using Content.Shared.Clothing;
using Content.Shared._CorvaxNext.CrewMedal;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using System.Linq;
using System.Text;

namespace Content.Server._CorvaxNext.CrewMedal;

public sealed class CrewMedalSystem : SharedCrewMedalSystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
		base.Initialize();
        SubscribeLocalEvent<CrewMedalComponent, ClothingGotEquippedEvent>(OnMedalEquipped);
        SubscribeLocalEvent<CrewMedalComponent, CrewMedalReasonChangedMessage>(OnMedalReasonChanged);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    /// <summary>
    /// Called when a medal is equipped on a character, indicating the medal has been awarded.
    /// </summary>
    private void OnMedalEquipped(Entity<CrewMedalComponent> medal, ref ClothingGotEquippedEvent args)
    {
        if (medal.Comp.Awarded)
            return;

        medal.Comp.Recipient = Identity.Name(args.Wearer, EntityManager);
        medal.Comp.Awarded = true;
        Dirty(medal);

        // Display a popup about the award
        _popupSystem.PopupEntity(
            Loc.GetString(
                "comp-crew-medal-award-text",
                ("recipient", medal.Comp.Recipient),
                ("medal", Name(medal.Owner))
            ),
            medal.Owner
        );

        // Log the event
        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Wearer):player} was awarded the {ToPrettyString(medal.Owner):entity} with the reason \"{medal.Comp.Reason}\"."
        );
    }

    /// <summary>
    /// Called when the reason is updated in the interface (before the medal is awarded).
    /// </summary>
    private void OnMedalReasonChanged(EntityUid uid, CrewMedalComponent medalComp, CrewMedalReasonChangedMessage args)
    {
        if (medalComp.Awarded)
            return;

        // Trim to the character limit and sanitize the input
        var maxLength = Math.Min(medalComp.MaxCharacters, args.Reason.Length);
        medalComp.Reason = Sanitize(args.Reason[..maxLength]);

        Dirty(uid, medalComp);

        // Log the update
        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):user} set {ToPrettyString(uid):entity} with award reason \"{medalComp.Reason}\"."
        );
    }

    /// <summary>
    /// Adds a list of awarded medals to the round-end summary window.
    /// </summary>
    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var awardedMedals = new List<(string MedalName, string RecipientName, string Reason)>();

        var query = EntityQueryEnumerator<CrewMedalComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Awarded)
            {
                awardedMedals.Add(
                    (Name(uid), component.Recipient, component.Reason)
                );
            }
        }

        if (awardedMedals.Count == 0)
            return;

        // Sort and convert to array
        var sortedMedals = awardedMedals.OrderBy(x => x.RecipientName).ToArray();

        var result = new StringBuilder();
        result.AppendLine(
            Loc.GetString(
                "comp-crew-medal-round-end-result",
                ("count", sortedMedals.Length)
            )
        );

        foreach (var medal in sortedMedals)
        {
            result.AppendLine(
                Loc.GetString(
                    "comp-crew-medal-round-end-list",
                    ("medal", Sanitize(medal.MedalName)),
                    ("recipient", Sanitize(medal.RecipientName)),
                    ("reason", Sanitize(medal.Reason))
                )
            );
        }

        ev.AddLine(result.AppendLine().ToString());
    }

    /// <summary>
    /// Removes certain prohibited characters (e.g., brackets) 
    /// to prevent unwanted tags in the text.
    /// </summary>
    private string Sanitize(string input)
    {
        return input
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Replace("{", string.Empty)
            .Replace("}", string.Empty);
    }
}

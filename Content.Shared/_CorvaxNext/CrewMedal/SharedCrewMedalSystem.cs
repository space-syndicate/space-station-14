using Content.Shared.Examine;

namespace Content.Shared._CorvaxNext.CrewMedal;

public abstract class SharedCrewMedalSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CrewMedalComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Displays the reason and recipient of an awarded medal during an Examine action.
    /// </summary>
    private void OnExamined(Entity<CrewMedalComponent> medal, ref ExaminedEvent args)
    {
        if (!medal.Comp.Awarded)
            return;

        var text = Loc.GetString(
            "comp-crew-medal-inspection-text",
            ("recipient", medal.Comp.Recipient),
            ("reason", medal.Comp.Reason)
        );

        args.PushMarkup(text);
    }
}

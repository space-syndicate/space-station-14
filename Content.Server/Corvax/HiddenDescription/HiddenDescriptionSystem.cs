using Content.Server.Mind;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Roles.Jobs;

namespace Content.Server.Corvax.HiddenDescription;

public sealed partial class HiddenDescriptionSystem : EntitySystem
{

    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiddenDescriptionComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<HiddenDescriptionComponent> hiddenDesc, ref ExaminedEvent args)
    {
        bool isJobAllow = job?.Prototype != null && item.JobRequired.Contains(job.Prototype.Value);
        bool isMindWhitelistPassed = item.WhitelistMind.IsValid(mindId);
        bool isBodyWhitelistPassed = item.WhitelistMind.IsValid(args.Examiner);

         bool shouldPushMarkup = item.NeedAllCheck
                ? isMindWhitelistPassed && isBodyWhitelistPassed && isJobAllow
                : isMindWhitelistPassed || isBodyWhitelistPassed || isJobAllow;

        if (shouldPushMarkup)
                args.PushMarkup(Loc.GetString(item.Label), hiddenDesc.Comp.PushPriority);
    }
}

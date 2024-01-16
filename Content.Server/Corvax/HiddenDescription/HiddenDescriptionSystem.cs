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
        var isGhost = TryComp<GhostComponent>(args.Examiner, out var _);

        _mind.TryGetMind(args.Examiner, out var mindId, out var mindComponent);
        TryComp<JobComponent>(mindId, out var job);

        foreach (var item in hiddenDesc.Comp.Entries)
        {
            //Show all secrets to the ghosts
            if (isGhost)
            {
                args.PushMarkup(Loc.GetString(item.Label), hiddenDesc.Comp.PushPriority);
                continue;
            }

            //Check job
            bool isJobAllow = false;
            if (job != null && job.Prototype != null)
            {
                if (item.JobRequired.Contains(job.Prototype.Value))
                {
                    isJobAllow = true;
                }
            }

            //Check mind to whitelist
            bool isWhitelistPassed = false;
            if (item.WhitelistMind.IsValid(mindId))
            {
                isWhitelistPassed = true;
            }

            //final check
            if (item.NeedBoth)
            {
                if (isWhitelistPassed && isJobAllow)
                    args.PushMarkup(Loc.GetString(item.Label), hiddenDesc.Comp.PushPriority);
            }
            else
            {
                if (isWhitelistPassed || isJobAllow)
                    args.PushMarkup(Loc.GetString(item.Label), hiddenDesc.Comp.PushPriority);
            }
        }
    }
}

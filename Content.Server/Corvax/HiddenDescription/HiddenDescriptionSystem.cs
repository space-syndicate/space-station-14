using Content.Server.Labels.Components;
using Content.Server.Mind;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Roles.Jobs;
using Content.Shared.Whitelist;

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
        var showAll = TryComp<GhostComponent>(args.Examiner, out var _);

        _mind.TryGetMind(args.Examiner, out var mindId, out var mindComponent);
        TryComp<JobComponent>(mindId, out var job);

        foreach (var element in hiddenDesc.Comp.MindWhitelistData)
        {
            if (element.Value.IsValid(mindId) || showAll)
                args.PushMarkup(Loc.GetString(element.Key));
        }

        foreach (var element in hiddenDesc.Comp.JobData)
        {
            //Show all secrets to the ghosts
            if (showAll)
            {
                args.PushMarkup(Loc.GetString(element.Key));
                continue;
            }

            //If job not exist or invalid - skip
            if (job == null || job.Prototype == null)
                continue;

            //Show secrets to the job
            if (element.Value.Contains(job.Prototype.Value))
                args.PushMarkup(Loc.GetString(element.Key));
        }
    }
}

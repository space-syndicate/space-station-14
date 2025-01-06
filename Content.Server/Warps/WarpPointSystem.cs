using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Ghost;

namespace Content.Server.Warps;

public sealed class WarpPointSystem : EntitySystem
{
    private Dictionary<string, EntityUid> warpPoints = new Dictionary<string, EntityUid>(); // Corvax-Next-Warper
	
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarpPointComponent, ExaminedEvent>(OnWarpPointExamine);
    }

    // Corvax-Next-Warper-Start
	public EntityUid? FindWarpPoint(string id) => IoCManager.Resolve<IEntityManager>().EntityQuery<WarpPointComponent>(true).FirstOrDefault(p => p.ID == id)?.Owner;
	// Corvax-Next-Warper-End

    private void OnWarpPointExamine(EntityUid uid, WarpPointComponent component, ExaminedEvent args)
    {
        if (!HasComp<GhostComponent>(args.Examiner))
            return;

        var loc = component.Location == null ? "<null>" : $"'{component.Location}'";
        args.PushText(Loc.GetString("warp-point-component-on-examine-success", ("location", loc)));
    }
}

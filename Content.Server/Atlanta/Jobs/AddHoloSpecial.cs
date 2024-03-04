using Content.Server.Guardian;
using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.Guardian;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Atlanta.Jobs;

/// <summary>
/// Adds holo on spawn to the entity
/// </summary>
[UsedImplicitly]
public sealed partial class AddHoloSpecial : JobSpecial
{
    [DataField("holo")]
    public EntProtoId Holo;

    public override void AfterEquip(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();

        var host = entMan.EnsureComponent<GuardianHostComponent>(mob);
        // Use map position so it's not inadvertantly parented to the host + if it's in a container it spawns outside I guess.
        var guardian = entMan.Spawn(Holo, entMan.GetComponent<TransformComponent>(mob).MapPosition);

        sysMan.GetEntitySystem<SharedContainerSystem>().Insert(guardian, host.GuardianContainer);
        host.HostedGuardian = guardian;

        if (entMan.TryGetComponent<GuardianComponent>(guardian, out var guardianComp))
        {
            guardianComp.Host = mob;
        }
    }
}

using System;
using Content.Server.Body.Systems;
using Content.Shared.Inventory.Events;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Shared.HardsuitIdentification;
using Content.Shared.Inventory;
using Content.Server.NPC.Systems;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Timer = Robust.Shared.Timing.Timer;
using Content.Server.NPC.Components;

namespace Content.Server.HardsuitIdentification;

public sealed class HardsuitIdentificationSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HardsuitIdentificationComponent, GotEquippedEvent>(OnEquip);
    }

    public void OnEquip(EntityUid uid, HardsuitIdentificationComponent component, GotEquippedEvent args)
    {
        var factionComp = EnsureComp<NpcFactionMemberComponent>(args.Equipee);

        foreach (var faction in new List<string>(factionComp.Factions))
        {
            if (faction == component.Faction)
            {
                return;
            }
        }

        if (component.Activated == true)
        {
            return;
        }

        component.Activated = true;

        _adminLogger.Add(LogType.Trigger, LogImpact.Medium,
            $"{ToPrettyString(args.Equipee):user} activated hardsuit self destruction system of {ToPrettyString(args.Equipment):target}");

        Timer.Spawn(500,
            () => _chat.TrySendInGameICMessage(args.Equipment, Loc.GetString("hardsuit-identification-error"), InGameICChatType.Speak, true));

        Timer.Spawn(1500,
            () => _chat.TrySendInGameICMessage(args.Equipment, "3", InGameICChatType.Speak, true));
        Timer.Spawn(2500,
            () => _chat.TrySendInGameICMessage(args.Equipment, "2", InGameICChatType.Speak, true));
        Timer.Spawn(3500,
            () => _chat.TrySendInGameICMessage(args.Equipment, "1", InGameICChatType.Speak, true));

        Timer.Spawn(4500,
            () =>
            {
                if (!EntityManager.EntityExists(args.Equipment))
                {
                    return;
                }

                _explosionSystem.QueueExplosion(Transform(args.Equipment).MapPosition, ExplosionSystem.DefaultExplosionPrototypeId,
                    4, 1, 2, maxTileBreak: 0);

                if (_inventory.TryGetSlotEntity(args.Equipee, "outerClothing", out var hardsuitEntity) && hardsuitEntity == args.Equipment)
                {
                    _bodySystem.GibBody(args.Equipee);
                }

                EntityManager.DeleteEntity(args.Equipment);
            });
    }
}

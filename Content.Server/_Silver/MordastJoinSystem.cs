using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Server.Traits.Assorted;
using Content.Shared.Administration.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Silver.MorDastJoin;

public sealed class MorDastJoinSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MordastJoinEvent>(MordastJoin);
    }

    private void OnMor_DastJoin(MordastJoinEvent args)
    {
        _chatManager.DispatchServerAnnouncement("Морда зашел", Color.Red);

    }

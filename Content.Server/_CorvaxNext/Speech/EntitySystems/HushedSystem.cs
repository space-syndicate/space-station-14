using Content.Server.Popups;
using Content.Server.Speech.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Shared.Speech;
using Content.Shared.Speech.Muting;
using Content.Shared.Speech.Hushing;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Puppet;
using Content.Shared.Actions;
using Robust.Shared.Player;

namespace Content.Server._CorvaxNext.Speech.EntitySystems;

public sealed class HushedSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HushedComponent, ScreamActionEvent>(OnScreamAction, before: [typeof(VocalSystem)]);
        SubscribeLocalEvent<HushedComponent, EmoteEvent>(OnEmote, before: [typeof(VocalSystem)]);
    }

    private void OnScreamAction(EntityUid uid, HushedComponent component, ScreamActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<MutedComponent>(uid))
            return;

        _popupSystem.PopupEntity(Loc.GetString("speech-hushed-scream-blocked"), uid, uid);
        args.Handled = true;
    }

    private void OnEmote(EntityUid uid, HushedComponent component, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<MutedComponent>(uid))
            return;

        if (!args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        _popupSystem.PopupEntity(Loc.GetString("speech-hushed-vocal-emote-blocked"), uid, uid);
        args.Handled = true;
    }
}

using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.Humanoid;
using Content.Server.Speech.Components;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class SnapSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SnapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SnapComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SnapComponent, SexChangedEvent>(OnSexChanged);
        SubscribeLocalEvent<SnapComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<SnapComponent, SnapActionEvent>(OnSnapAction);
    }

    private void OnMapInit(EntityUid uid, SnapComponent component, MapInitEvent args)
    {
        // try to add Snap action when Snap comp added
        if (_proto.TryIndex(component.SnapActionId, out InstantActionPrototype? proto))
        {
            component.SnapAction = new InstantAction(proto);
            _actions.AddAction(uid, component.SnapAction, null);
        }

        LoadSounds(uid, component);
    }

    private void OnShutdown(EntityUid uid, SnapComponent component, ComponentShutdown args)
    {
        // remove Snap action when component removed
        if (component.SnapAction != null)
        {
            _actions.RemoveAction(uid, component.SnapAction);
        }
    }

    private void OnSexChanged(EntityUid uid, SnapComponent component, SexChangedEvent args)
    {
        LoadSounds(uid, component);
    }

    private void OnEmote(EntityUid uid, SnapComponent component, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.General))
            return;

        // snowflake case for wilhelm Snap easter egg
        if (args.Emote.ID == component.SnapId)
        {
            args.Handled = TryPlaySnapSound(uid, component);
            return;
        }

        // just play regular sound based on emote proto
        args.Handled = _chat.TryPlayEmoteSound(uid, component.EmoteSounds, args.Emote);
    }

    private void OnSnapAction(EntityUid uid, SnapComponent component, SnapActionEvent args)
    {
        if (args.Handled)
            return;

        _chat.TryEmoteWithChat(uid, component.SnapActionId);
        args.Handled = true;
    }

    private bool TryPlaySnapSound(EntityUid uid, SnapComponent component)
    {
        if (_random.Prob(component.SnapProbability))
        {
            _audio.PlayPvs(component.Snap, uid, component.Snap.Params);
            return true;
        }

        return _chat.TryPlayEmoteSound(uid, component.EmoteSounds, component.SnapId);
    }

    private void LoadSounds(EntityUid uid, SnapComponent component, Sex? sex = null)
    {
        if (component.Sounds == null)
            return;

        sex ??= CompOrNull<HumanoidAppearanceComponent>(uid)?.Sex ?? Sex.Unsexed;

        if (!component.Sounds.TryGetValue(sex.Value, out var protoId))
            return;
        _proto.TryIndex(protoId, out component.EmoteSounds);
    }
}

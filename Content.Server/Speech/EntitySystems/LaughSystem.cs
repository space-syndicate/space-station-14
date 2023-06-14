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

public sealed class LaughSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LaughComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LaughComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LaughComponent, SexChangedEvent>(OnSexChanged);
        SubscribeLocalEvent<LaughComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<LaughComponent, LaughActionEvent>(OnLaughAction);
    }

    private void OnMapInit(EntityUid uid, LaughComponent component, MapInitEvent args)
    {
        // try to add Laugh action when Laugh comp added
        if (_proto.TryIndex(component.LaughActionId, out InstantActionPrototype? proto))
        {
            component.LaughAction = new InstantAction(proto);
            _actions.AddAction(uid, component.LaughAction, null);
        }

        LoadSounds(uid, component);
    }

    private void OnShutdown(EntityUid uid, LaughComponent component, ComponentShutdown args)
    {
        // remove Laugh action when component removed
        if (component.LaughAction != null)
        {
            _actions.RemoveAction(uid, component.LaughAction);
        }
    }

    private void OnSexChanged(EntityUid uid, LaughComponent component, SexChangedEvent args)
    {
        LoadSounds(uid, component);
    }

    private void OnEmote(EntityUid uid, LaughComponent component, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.General))
            return;

        // snowflake case for wilhelm Laugh easter egg
        if (args.Emote.ID == component.LaughId)
        {
            args.Handled = TryPlayLaughSound(uid, component);
            return;
        }

        // just play regular sound based on emote proto
        args.Handled = _chat.TryPlayEmoteSound(uid, component.EmoteSounds, args.Emote);
    }

    private void OnLaughAction(EntityUid uid, LaughComponent component, LaughActionEvent args)
    {
        if (args.Handled)
            return;

        _chat.TryEmoteWithChat(uid, component.LaughActionId);
        args.Handled = true;
    }

    private bool TryPlayLaughSound(EntityUid uid, LaughComponent component)
    {
        if (_random.Prob(component.LaughProbability))
        {
            _audio.PlayPvs(component.Laugh, uid, component.Laugh.Params);
            return true;
        }

        return _chat.TryPlayEmoteSound(uid, component.EmoteSounds, component.LaughId);
    }

    private void LoadSounds(EntityUid uid, LaughComponent component, Sex? sex = null)
    {
        if (component.Sounds == null)
            return;

        sex ??= CompOrNull<HumanoidAppearanceComponent>(uid)?.Sex ?? Sex.Unsexed;

        if (!component.Sounds.TryGetValue(sex.Value, out var protoId))
            return;
        _proto.TryIndex(protoId, out component.EmoteSounds);
    }
}

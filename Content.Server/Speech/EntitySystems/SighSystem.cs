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

public sealed class SighSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SighComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SighComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SighComponent, SexChangedEvent>(OnSexChanged);
        SubscribeLocalEvent<SighComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<SighComponent, SighActionEvent>(OnSighAction);
    }

    private void OnMapInit(EntityUid uid, SighComponent component, MapInitEvent args)
    {
        // try to add Sigh action when Sigh comp added
        if (_proto.TryIndex(component.SighActionId, out InstantActionPrototype? proto))
        {
            component.SighAction = new InstantAction(proto);
            _actions.AddAction(uid, component.SighAction, null);
        }

        LoadSounds(uid, component);
    }

    private void OnShutdown(EntityUid uid, SighComponent component, ComponentShutdown args)
    {
        // remove Sigh action when component removed
        if (component.SighAction != null)
        {
            _actions.RemoveAction(uid, component.SighAction);
        }
    }

    private void OnSexChanged(EntityUid uid, SighComponent component, SexChangedEvent args)
    {
        LoadSounds(uid, component);
    }

    private void OnEmote(EntityUid uid, SighComponent component, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.General))
            return;

        // snowflake case for wilhelm Sigh easter egg
        if (args.Emote.ID == component.SighId)
        {
            args.Handled = TryPlaySighSound(uid, component);
            return;
        }

        // just play regular sound based on emote proto
        args.Handled = _chat.TryPlayEmoteSound(uid, component.EmoteSounds, args.Emote);
    }

    private void OnSighAction(EntityUid uid, SighComponent component, SighActionEvent args)
    {
        if (args.Handled)
            return;

        _chat.TryEmoteWithChat(uid, component.SighActionId);
        args.Handled = true;
    }

    private bool TryPlaySighSound(EntityUid uid, SighComponent component)
    {
        if (_random.Prob(component.SighProbability))
        {
            _audio.PlayPvs(component.Sigh, uid, component.Sigh.Params);
            return true;
        }

        return _chat.TryPlayEmoteSound(uid, component.EmoteSounds, component.SighId);
    }

    private void LoadSounds(EntityUid uid, SighComponent component, Sex? sex = null)
    {
        if (component.Sounds == null)
            return;

        sex ??= CompOrNull<HumanoidAppearanceComponent>(uid)?.Sex ?? Sex.Unsexed;

        if (!component.Sounds.TryGetValue(sex.Value, out var protoId))
            return;
        _proto.TryIndex(protoId, out component.EmoteSounds);
    }
}

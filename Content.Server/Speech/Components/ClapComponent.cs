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

public sealed class ClapSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClapComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ClapComponent, SexChangedEvent>(OnSexChanged);
        SubscribeLocalEvent<ClapComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<ClapComponent, ClapActionEvent>(OnClapAction);
    }

    private void OnMapInit(EntityUid uid, ClapComponent component, MapInitEvent args)
    {
        // try to add Clap action when Clap comp added
        if (_proto.TryIndex(component.ClapActionId, out InstantActionPrototype? proto))
        {
            component.ClapAction = new InstantAction(proto);
            _actions.AddAction(uid, component.ClapAction, null);
        }

        LoadSounds(uid, component);
    }

    private void OnShutdown(EntityUid uid, ClapComponent component, ComponentShutdown args)
    {
        // remove Clap action when component removed
        if (component.ClapAction != null)
        {
            _actions.RemoveAction(uid, component.ClapAction);
        }
    }

    private void OnSexChanged(EntityUid uid, ClapComponent component, SexChangedEvent args)
    {
        LoadSounds(uid, component);
    }

    private void OnEmote(EntityUid uid, ClapComponent component, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.General))
            return;

        // snowflake case for wilhelm Clap easter egg
        if (args.Emote.ID == component.ClapId)
        {
            args.Handled = TryPlayClapSound(uid, component);
            return;
        }

        // just play regular sound based on emote proto
        args.Handled = _chat.TryPlayEmoteSound(uid, component.EmoteSounds, args.Emote);
    }

    private void OnClapAction(EntityUid uid, ClapComponent component, ClapActionEvent args)
    {
        if (args.Handled)
            return;

        _chat.TryEmoteWithChat(uid, component.ClapActionId);
        args.Handled = true;
    }

    private bool TryPlayClapSound(EntityUid uid, ClapComponent component)
    {
        if (_random.Prob(component.ClapProbability))
        {
            _audio.PlayPvs(component.Clap, uid, component.Clap.Params);
            return true;
        }

        return _chat.TryPlayEmoteSound(uid, component.EmoteSounds, component.ClapId);
    }

    private void LoadSounds(EntityUid uid, ClapComponent component, Sex? sex = null)
    {
        if (component.Sounds == null)
            return;

        sex ??= CompOrNull<HumanoidAppearanceComponent>(uid)?.Sex ?? Sex.Unsexed;

        if (!component.Sounds.TryGetValue(sex.Value, out var protoId))
            return;
        _proto.TryIndex(protoId, out component.EmoteSounds);
    }
}

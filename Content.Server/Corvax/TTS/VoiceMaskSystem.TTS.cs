using Content.Shared.Corvax.TTS;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.VoiceMask;

namespace Content.Server.VoiceMask;

public partial class VoiceMaskSystem
{
    private void InitializeTTS()
    {
        SubscribeLocalEvent<VoiceMaskComponent, InventoryRelayedEvent<TransformSpeakerVoiceEvent>>(OnSpeakerVoiceTransform);
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskChangeVoiceMessage>(OnChangeVoice);
        SubscribeLocalEvent<VoiceMaskComponent, ImplantRelayEvent<TransformSpeakerVoiceEvent>>(OnSpeakerVoiceTransformImplant);
    }

    private void OnSpeakerVoiceTransform(EntityUid uid, VoiceMaskComponent component, InventoryRelayedEvent<TransformSpeakerVoiceEvent> args)
    {
        if (!component.Active)
            return;
        args.Args.VoiceId = component.VoiceId;
    }

    private void OnChangeVoice(Entity<VoiceMaskComponent> entity, ref VoiceMaskChangeVoiceMessage msg)
    {
        if (msg.Voice is { } id && !_proto.HasIndex<TTSVoicePrototype>(id))
            return;

        entity.Comp.VoiceId = msg.Voice;

        _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-success"), entity);

        UpdateUI(entity);
    }
    private void OnSpeakerVoiceTransformImplant(EntityUid uid, VoiceMaskComponent component, ImplantRelayEvent<TransformSpeakerVoiceEvent> args)
    {
        if (!component.Active)
            return;
        args.Event.VoiceId = component.VoiceId;
    }
}

using Content.Server.Chat.Systems;
using Content.Server.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

/// <summary>
///     This system redirects local chat messages to listening entities (e.g., radio microphones).
/// </summary>
public sealed class ListeningSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xforms = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpeak);
    }

    private void OnSpeak(EntitySpokeEvent ev)
    {
		if (HasComp<SecretMessageSourceComponent>(ev.Source))
			PingListeners(ev.Source, ev.Message, ev.ObfuscatedMessage, true);
		else
			PingListeners(ev.Source, ev.Message, ev.ObfuscatedMessage, false);
    }

    public void PingListeners(EntityUid source, string message, string? obfuscatedMessage, bool secretMessage)
    {
        // TODO whispering / audio volume? Microphone sensitivity?
        // for now, whispering just arbitrarily reduces the listener's max range.

        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = xformQuery.GetComponent(source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);

        var attemptEv = new ListenAttemptEvent(source);
        var ev = new ListenEvent(message, source);
        var obfuscatedEv = obfuscatedMessage == null ? null : new ListenEvent(obfuscatedMessage, source);
        var query = EntityQueryEnumerator<ActiveListenerComponent, TransformComponent>();
		
		if (secretMessage){
			while(query.MoveNext(out var listenerUid, out var listener, out var xform))
			{
				if (xform.MapID != sourceXform.MapID)
					continue;

				// range checks
				// TODO proper speech occlusion
				var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).LengthSquared();
				if ((distance > listener.Range * listener.Range) || HasComp<SecretMessageListenerComponent>(listenerUid))
					continue;

				RaiseLocalEvent(listenerUid, attemptEv);
				if (attemptEv.Cancelled)
				{
					attemptEv.Uncancel();
					continue;
				}

				if (obfuscatedEv != null && distance > ChatSystem.WhisperClearRange)
					RaiseLocalEvent(listenerUid, obfuscatedEv);
				else
					RaiseLocalEvent(listenerUid, ev);
			}
		}
		else{
			while(query.MoveNext(out var listenerUid, out var listener, out var xform))
			{
				if (xform.MapID != sourceXform.MapID)
					continue;

				// range checks
				// TODO proper speech occlusion
				var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).LengthSquared();
				if (distance > listener.Range * listener.Range)
					continue;

				RaiseLocalEvent(listenerUid, attemptEv);
				if (attemptEv.Cancelled)
				{
					attemptEv.Uncancel();
					continue;
				}

				if (obfuscatedEv != null && distance > ChatSystem.WhisperClearRange)
					RaiseLocalEvent(listenerUid, obfuscatedEv);
				else
					RaiseLocalEvent(listenerUid, ev);
			}
		}
    }
}

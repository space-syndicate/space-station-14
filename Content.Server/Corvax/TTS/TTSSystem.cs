using System.Linq;
using System.Threading.Tasks;
using Content.Server.Communications;
using Content.Server.Power.Components;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Station.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Speech.Muting;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetConfigurationManager _netCfg = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private TTSManager _ttsManager = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private IRobustRandom _rng = default!;

    private readonly List<string> _sampleText = new()
    {
        // Neutral / Declarative
        "Съешь же ещё этих мягких французских булок, да выпей чаю.",
        "Инженеры закончили настройку сингулярности, теперь всё работает стабильно.",
        "Квартирмейстер подтвердил заказ на новую партию оборудования.",

        // Interrogative / Questions (rising intonation at the end)
        "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
        "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
        "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
        "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
        "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах?",

        // Exclamatory / Emotional (emphasis on UPPERCASE words)
        "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! ПОМОГИТЕ!!",
        "Учёные, тут странная аномалия в баре! Она уже съела МИМА!",
        "Возле эвакуационного шаттла РАЗГЕРМЕТИЗАЦИЯ! Инженеры, нам СРОЧНО нужна ваша помощь!",
        "Капитан, КЛОУН разбрасывает банановые кожурки под ноги офицерам!",

        // Mixed / Question + Exclamation
        "Ты серьёзно думаешь, что это хорошая идея?!",
        "Что ты делаешь?! Немедленно прекрати!",
        "Ты это видел?! Это было невероятно!",

        // Ellipsis / Pauses for Suspense
        "Я думаю... нам стоит пересмотреть этот план...",
        "Странно... я только что видел здесь кого-то... но никого нет...",
        "Командир... я должен вам кое-что сказать... это важно...",

        // Strong Emphasis (НЕТ / ДА / НЕ)
        "НЕТ! Я НЕ пойду в этот отсек! Это СЛИШКОМ опасно!",
        "ДА! Мы сделали это! ПОБЕДА!",
        "Я ТРЕБУЮ! Немедленно прекратить эксперименты!",

        // Short Radio / Command Style
        "Внимание всем! Переходим на аварийный режим работы!",
        "Приём! Требуется подкрепление в зоне мостика!",

        // Long Sentences / Breath Pauses
        "Я хочу чтобы вы знали, что эта станция лучшая во всём секторе, и каждый из вас вносит огромный вклад в наше общее дело, поэтому я горжусь вами.",

        // Lists / Enumeration
        "Что нам нужно сделать? Во-первых, проверить системы; во-вторых, подготовить отчёт; и в-третьих, доложить командованию.",
        "В ящике лежат: инструмент, медицинские наборы и противогазы.",

        // Calm / Reassuring
        "Не волнуйтесь, я контролирую ситуацию, всё будет хорошо.",
        "Сохраняйте спокойствие, мы уже на подходе к решению.",
        // Да я подписал все на английском и чо? Вчіть мову
    };

    private static readonly ProtoId<TTSVoicePrototype> AnnouncementSpeaker = "Glados";
    private const int MaxMessageChars = 100 * 2; // same as SingleBubbleCharLimit * 2
    private const float AnnouncementDelay = 2.25f;
    private bool _isEnabled = false;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<CommunicationConsoleAnnouncementEvent>(OnConsoleAnnouncement);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke,
            before: [typeof(RadioSystem), typeof(HeadsetSystem)]); // Before the channel is cleared

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        RegisterRateLimits();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
        => _ttsManager.ResetCache();

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled || !ProtoMan.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession),
            recordReplay: false);
    }

    private void OnConsoleAnnouncement(ref CommunicationConsoleAnnouncementEvent ev)
    {
        if (!_isEnabled || string.IsNullOrEmpty(ev.Text))
            return;

        var station = _stationSystem.GetOwningStation(ev.Sender);
        if (station == null)
            return;

        if (!HasComp<StationDataComponent>(station))
            return;

        TTSVoicePrototype? voicePrototype = null;
        if (TryComp<TTSComponent>(ev.Sender, out var ttsComp) && !HasComp<MutedStatusEffectComponent>(ev.Sender))
        {
            if (!string.IsNullOrEmpty(ttsComp.VoicePrototypeId))
            {
                ProtoMan.TryIndex(ttsComp.VoicePrototypeId, out voicePrototype);
            }
        }

        if (voicePrototype == null)
        {
            if (!ProtoMan.TryIndex(AnnouncementSpeaker, out voicePrototype))
                return;
        }

        HandleConsoleAnnouncement(ev.Text, voicePrototype.Speaker, ev.Component.Sound, station.Value);
    }

    private async void HandleConsoleAnnouncement(string text, string speaker,
        SoundSpecifier sound, EntityUid station)
    {
        var textSanitized = Sanitize(text);
        if (string.IsNullOrEmpty(textSanitized))
            return;

        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast | SoundTraits.PitchMedium;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        var soundData = await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
        if (soundData is null)
            return;

        var timeDelay = (float)_audio.GetAudioLength(_audio.ResolveSound(sound)).TotalSeconds + AnnouncementDelay;

        Timer.Spawn(TimeSpan.FromSeconds(timeDelay), () =>
        {
            var filter = GetStationFilter(station);
            if (filter == null)
                return;

            RaiseNetworkEvent(new PlayTTSEvent(soundData), filter,
                recordReplay: false);
        });
    }

    private Filter? GetStationFilter(Entity<StationDataComponent?> station)
    {
        if (!Resolve(station, ref station.Comp, false))
            return null;

        return _stationSystem.GetInStation(station.Comp);
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled || string.IsNullOrEmpty(voiceId))
            return;

        if (args.Message.Length > MaxMessageChars)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!ProtoMan.TryIndex(voiceId, out var protoVoice))
            return;

        if (args.ObfuscatedMessage != null)
        {
            HandleWhisper(uid, args.Message, args.ObfuscatedMessage, protoVoice.Speaker, args.Channel);
            return;
        }

        HandleSay(uid, args.Message, protoVoice.Speaker, args.Channel);
    }

    private async void HandleSay(EntityUid uid, string message, string speaker, RadioChannelPrototype? channel)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null) return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), Filter.Pvs(uid),
            recordReplay: false);

        if (channel != null)
        {
            SendTTSToRadio(soundData, uid, channel, false);
        }
    }

    private async void HandleWhisper(EntityUid uid, string message, string obfMessage, string speaker,
        RadioChannelPrototype? channel)
    {
        var fullSoundData = await GenerateTTS(message, speaker, true);
        if (fullSoundData is null) return;

        // I never saw the point of voicing just four or five letters in a long message, only to get a jumbled mess in response.
        // Response "~ ~~~~ ~~~ пыр-~ы~-~~~" this is the most useless waste of money I've ever seen.
        // var obfSoundData = await GenerateTTS(obfMessage, speaker, true);
        // if (obfSoundData is null) return;

        var fullTtsEvent = new PlayTTSEvent(fullSoundData, GetNetEntity(uid), true);
        // var obfTtsEvent = new PlayTTSEvent(obfSoundData, GetNetEntity(uid), true);

        // TODO: Check obstacles
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourcePos = _xforms.GetWorldPosition(xformQuery.GetComponent(uid), xformQuery);
        var receptions = Filter.Pvs(uid).Recipients;
        var clearFilter = Filter.Empty();
        // var obfFilter = Filter.Empty();

        foreach (var session in receptions)
        {
            if (!session.AttachedEntity.HasValue) continue;
            var xform = xformQuery.GetComponent(session.AttachedEntity.Value);
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();
            if (distance > SharedChatSystem.WhisperClearRange)
                continue;

            clearFilter.AddPlayer(session);
        }

        if (clearFilter.Recipients.Any())
        {
            RaiseNetworkEvent(fullTtsEvent, clearFilter, recordReplay: false);
        }

        // if (obfFilter.Recipients.Any())
        // {
        //     RaiseNetworkEvent(obfTtsEvent, obfFilter, recordReplay: false);
        // }

        if (channel != null)
        {
            SendTTSToRadio(fullSoundData, uid, channel);
        }
    }

    private void SendTTSToRadio(byte[] soundData, EntityUid sourceUid, RadioChannelPrototype channel, bool isWhisper = true)
    {
        var channelFlag = GetChannelFlag(channel.ID);
        if (channelFlag == RadioChannelFlag.None)
            return; // Unknown - Skip

        var netSource = GetNetEntity(sourceUid);
        var ttsEvent = new PlayTTSEvent(soundData, netSource, isWhisper, true);
        var filter = Filter.Empty();

        var sourceMapId = Transform(sourceUid).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);

        var query = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (query.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels && !radio.Channels.Contains(channel.ID))
                continue;

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            var needServer = !channel.LongRange && !HasComp<TelecomExemptComponent>(receiver);
            if (needServer && !hasActiveServer)
                continue;

            var attemptEv = new RadioReceiveAttemptEvent(channel, sourceUid, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            EntityUid? wearer = null;

            // Receiver could be, for example, a Borg
            if (TryComp(receiver, out ActorComponent? actor)
                && !HasComp<GhostComponent>(receiver)) // Save the ghosts ears
            {
                wearer = receiver;
            }
            // Wearer is the entity currently wearing the headset
            else if (TryComp<HeadsetComponent>(receiver, out var headset))
            {
                if (!headset.Enabled || !headset.IsEquipped)
                    continue;

                wearer = transform.ParentUid;
            }

            if (wearer == null)
                continue;

            if (!TryComp(wearer.Value, out actor))
                continue;

            var session = actor.PlayerSession;
            if (session.AttachedEntity == sourceUid)
                continue;

            var playerFilter = _netCfg.GetClientCVar(session.Channel, CCCVars.TTSRadioFilter);
            var playerFlag = (RadioChannelFlag)playerFilter;
            if (!playerFlag.HasFlag(channelFlag))
                continue;

            filter.AddPlayer(session);
        }

        if (!filter.Recipients.Any())
            return;

        RaiseNetworkEvent(ttsEvent, filter, recordReplay: false);
    }

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId && power.Powered
                && keys.Channels.Contains(channelId))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Maps channel prototype ID to RadioChannelFlag.
    /// </summary>
    private RadioChannelFlag GetChannelFlag(string channelId)
    {
        return channelId switch
        {
            "Common" => RadioChannelFlag.Common,
            "Command" => RadioChannelFlag.Command,
            "Engineering" => RadioChannelFlag.Engineering,
            "Medical" => RadioChannelFlag.Medical,
            "Science" => RadioChannelFlag.Science,
            "Security" => RadioChannelFlag.Security,
            "Service" => RadioChannelFlag.Service,
            "Supply" => RadioChannelFlag.Supply,
            "Legal" => RadioChannelFlag.Legal,
            "Syndicate" => RadioChannelFlag.Syndicate,
            "Binary" => RadioChannelFlag.Binary,
            "Handheld" => RadioChannelFlag.Handheld,
            "Freelance" => RadioChannelFlag.Freelance,
            "CentCom" => RadioChannelFlag.CentCom,
            "Xenoborg" => RadioChannelFlag.Xenoborg,
            "Mothership" => RadioChannelFlag.Mothership,
            _ => RadioChannelFlag.None
        };
    }

    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        SoundTraits ssmlTraits;
        if (isWhisper)
        {
            ssmlTraits = SoundTraits.RateSlow | SoundTraits.PitchVerylow | SoundTraits.VolumeXSoft;
        }
        else
        {
            ssmlTraits = SoundTraits.RateFast | SoundTraits.PitchMedium;
        }

        var textSsml = ToSsmlText(textSanitized, ssmlTraits);
        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}

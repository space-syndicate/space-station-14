using System.Linq;
using System.Threading.Tasks;
using Content.Server.Communications;
using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
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
using Content.Shared.Ghost.Components;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetConfigurationManager _netCfg = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private TTSManager _ttsManager = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private IRobustRandom _rng = default!;

    private readonly HashSet<string> _sampleText = new()
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
    private const float TTSRange = SharedChatSystem.VoiceRange * 1.5f;
    private bool _isEnabled;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.TTSEnabled, v => _isEnabled = v, true);

        RegisterRateLimits();
    }

    [SubscribeLocalEvent]
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
        => _ttsManager.ResetCache();

    [SubscribeNetworkEvent]
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

        RaiseNetworkEvent(new PlayTTSEvent(soundData),
            Filter.SinglePlayer(args.SenderSession),
            recordReplay: false);
    }

    [SubscribeLocalEvent]
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

    private async void HandleConsoleAnnouncement(
        string text,
        string speaker,
        SoundSpecifier sound,
        EntityUid station)
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

        Timer.Spawn(TimeSpan.FromSeconds(timeDelay),
            () =>
        {
            var filter = GetStationFilter(station);
            if (filter == null)
                return;

            RaiseNetworkEvent(new PlayTTSEvent(soundData),
                filter,
                recordReplay: false);
        });
    }

    private Filter? GetStationFilter(Entity<StationDataComponent?> station)
    {
        return !Resolve(station, ref station.Comp, false) ? null : _stationSystem.GetInStation(station.Comp);
    }

    [SubscribeLocalEvent(before: [typeof(RadioSystem), typeof(HeadsetSystem)])]  // Before the channel is cleared
    private void OnEntitySpoke(Entity<TTSComponent> ent, ref EntitySpokeEvent args)
    {
        var voiceId = ent.Comp.VoicePrototypeId;
        if (!_isEnabled || string.IsNullOrEmpty(voiceId))
            return;

        if (args.Message.Length > MaxMessageChars)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(ent, voiceId);
        RaiseLocalEvent(ent, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!ProtoMan.TryIndex(voiceId, out var protoVoice))
            return;

        if (args.ObfuscatedMessage != null)
        {
            HandleWhisper(ent, args.Message, protoVoice.Speaker, args.Channel);
            return;
        }

        HandleSay(ent, args.Message, protoVoice.Speaker, args.Channel);
    }

    private async void HandleSay(EntityUid uid, string message, string speaker, RadioChannelPrototype? channel)
    {
        var soundData = await GenerateTTS(message, speaker);

        if (soundData is null)
            return;

        // Should be here because EntitySpokeEvent may be called on the entities in the PVS range, but not always in range where player receives chat messages
        var filter = GetReceiversFilter(uid, TTSRange);
        if (filter.Recipients.Any())
        {
            RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), filter, recordReplay: false);
        }

        if (channel != null)
        {
            SendTTSToRadio(soundData, uid, channel, false);
        }
    }

    private async void HandleWhisper(
        EntityUid uid,
        string message,
        string speaker,
        RadioChannelPrototype? channel)
    {
        var fullSoundData = await GenerateTTS(message, speaker, true);
        if (fullSoundData is null)
            return;

        var fullTtsEvent = new PlayTTSEvent(fullSoundData, GetNetEntity(uid), true);

        // TODO: Check obstacles
        var filter = GetReceiversFilter(uid, SharedChatSystem.WhisperClearRange);
        if (filter.Recipients.Any())
        {
            RaiseNetworkEvent(fullTtsEvent, filter, recordReplay: false);
        }

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
    private static RadioChannelFlag GetChannelFlag(string channelId)
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
            _ => RadioChannelFlag.None,
        };
    }

    // TODO: Check obstacles
    private Filter GetReceiversFilter(EntityUid sourceUid, float range)
    {
        var pvs = Filter.Pvs(sourceUid);
        var filter = Filter.Empty();

        foreach (var player in pvs.Recipients)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            var transformEntity = Transform(playerEntity);

            if (transformEntity.MapID != Transform(sourceUid).MapID)
                continue;

            if (!Transform(sourceUid).Coordinates.TryDistance(EntityManager, transformEntity.Coordinates, out var distance)
                || distance >= range)
                continue;

            filter.AddPlayer(player);
        }

        return filter;
    }

    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);

        if (string.IsNullOrEmpty(textSanitized))
            return null;

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

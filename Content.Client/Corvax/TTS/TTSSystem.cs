using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private IRobustRandom _ran = default!;
    [Dependency] private AudioSystem _audio = default!;

    private ISawmill _sawmill = default!;
    private static MemoryContentRoot _contentRoot = new();
    private static readonly ResPath Prefix = ResPath.Root / "TTS";
    private static bool _contentRootAdded;

    private const float WhisperFade = 4f;
    private const float MinimalVolume = -10f;
    private const float GlobalVolumeBonus = 1.5f;
    private const float RadioPitchMin = 0.95f;
    private const float RadioPitchMax = 1.02f;
    private const float RadioVariationMin = 0.005f;
    private const float RadioVariationMax = 0.025f;
    private const float RadioRolloffMin = 1.5f;
    private const float RadioRolloffMax = 2.5f;
    private const float PlaybackDelay = 0.8f;

    private float _lastRadioPitch = 0.98f;
    private float _radioVolume = 1.2f;
    private float _volume = 1.2f;

    private readonly HashSet<NetEntity> _playingEntities = new();
    private readonly Dictionary<NetEntity, Queue<PlayTTSEvent>> _entityQueues = new();
    private TTSVoiceEffectPreset _voiceEffectPreset = TTSVoiceEffectPreset.None;
    private int _fileIdx = 0;

    public override void Initialize()
    {
        base.Initialize();
        if (!_contentRootAdded)
        {
            _contentRootAdded = true;
            _res.AddRoot(Prefix, _contentRoot);
        }

        _sawmill = Logger.GetSawmill("tts");
        _cfg.OnValueChanged(CCCVars.TTSVoiceEffect, OnVoiceEffectChanged, true);
        _cfg.OnValueChanged(CCCVars.TTSRadioVolume, OnRadioVolumeChanged, true);
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnVolumeChanged, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(CCCVars.TTSVoiceEffect, OnVoiceEffectChanged);
        _cfg.UnsubValueChanged(CCCVars.TTSRadioVolume, OnRadioVolumeChanged);
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnVolumeChanged);

        _entityQueues.Clear();
        _playingEntities.Clear();

        ShutdownEffects();
    }

    private void OnVoiceEffectChanged(int newValue)
    {
        _voiceEffectPreset = (TTSVoiceEffectPreset)newValue;

        if (_cachedVoiceEffectEntity != null)
        {
            if (!TerminatingOrDeleted(_cachedVoiceEffectEntity.Value))
                Del(_cachedVoiceEffectEntity.Value);

            _cachedVoiceEffectEntity = null;
        }

        if (_voiceEffectPreset != TTSVoiceEffectPreset.None)
            EnsureVoiceEffectInitialized();
    }

    private void OnVolumeChanged(float value)
    {
        _volume = value;
    }

    private void OnRadioVolumeChanged(float value)
    {
        _radioVolume = value;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _entityQueues.Clear();
        _playingEntities.Clear();

        ShutdownEffects();
    }

    public void RequestPreviewTTS(string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        // It will stop clogging up your memory if you turn off one of the sliders to 0
        if (ev.IsRadio && _radioVolume <= 0)
        {
            _sawmill.Verbose("Radio TTS volume zero, skipping playback");
            return;
        }
        else if (_volume <= 0)
        {
            _sawmill.Verbose("TTS volume zero, skipping playback");
            return;
        }

        if (ev.SourceUid == null)
        {
            PlayTTSInternal(ev);
            return;
        }

        var sourceUid = ev.SourceUid.Value;

        lock (_entityQueues)
        {
            if (!_entityQueues.TryGetValue(sourceUid, out var queue))
            {
                queue = new Queue<PlayTTSEvent>();
                _entityQueues[sourceUid] = queue;
            }

            if (queue.Count >= 6)
            {
                _sawmill.Verbose($"TTS queue for {sourceUid} is full, dropping old message");
                queue.Dequeue();
                queue.Enqueue(ev);
                return;
            }

            queue.Enqueue(ev);
            _sawmill.Verbose($"TTS added to queue for entity {sourceUid}. Queue size: {queue.Count}");
        }

        if (!_playingEntities.Contains(sourceUid))
        {
            ProcessNextInQueueForEntity(sourceUid);
        }
    }

    private void ProcessNextInQueueForEntity(NetEntity entityUid)
    {
        PlayTTSEvent? ev = null;

        lock (_entityQueues)
        {
            if (_entityQueues.TryGetValue(entityUid, out var queue) && queue.Count > 0)
            {
                ev = queue.Dequeue();
                _playingEntities.Add(entityUid);
            }
            else
            {
                _playingEntities.Remove(entityUid);
                if (queue != null && queue.Count == 0)
                    _entityQueues.Remove(entityUid);

                return;
            }
        }

        if (ev == null)
        {
            _playingEntities.Remove(entityUid);
            return;
        }

        try
        {
            PlayTTSInternal(ev, () =>
            {
                _playingEntities.Remove(entityUid);
                ProcessNextInQueueForEntity(entityUid);
            });
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error playing TTS for entity {entityUid}: {ex.Message}");
            _playingEntities.Remove(entityUid);

            ProcessNextInQueueForEntity(entityUid);
        }
    }

    private void PlayTTSInternal(PlayTTSEvent ev, Action? onComplete = null)
    {
        var filePath = new ResPath($"{_fileIdx++}.ogg");
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);

        using var audioResource = new AudioResource();
        audioResource.Load(IoCManager.Instance!, Prefix / filePath);

        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.SourceUid == null, ev.IsWhisper, ev.IsRadio))
            .WithMaxDistance(AdjustDistance(ev.IsWhisper));

        var soundSpecifier = new ResolvedPathSpecifier(Prefix / filePath);

        (EntityUid Entity, AudioComponent Component)? audioResult = null;

        try
        {
            if (ev.IsRadio)
            {
                var pitch = GetRadioPitch();
                var variation = GetRadioVariation();
                var rolloff = GetRadioRolloff();

                var radioParams = audioParams
                    .WithRolloffFactor(rolloff)
                    .WithVariation(variation)
                    .WithPitchScale(pitch);

                PlayRadioWithEffectInternal(audioResource, soundSpecifier, radioParams);
            }
            else if (ev.SourceUid != null)
            {
                var sourceUid = GetEntity(ev.SourceUid.Value);
                if (TerminatingOrDeleted(sourceUid))
                {
                    onComplete?.Invoke();
                    return;
                }

                audioResult = _audio.PlayEntity(audioResource.AudioStream, sourceUid, soundSpecifier, audioParams);
                if (audioResult != null && _voiceEffectPreset != 0)
                {
                    ApplyVoiceEffect(audioResult.Value, _voiceEffectPreset);
                }
            }
            else
            {
                audioResult = _audio.PlayGlobal(audioResource.AudioStream, soundSpecifier, audioParams);
                if (audioResult != null && _voiceEffectPreset != 0)
                {
                    ApplyVoiceEffect(audioResult.Value, _voiceEffectPreset);
                }
            }
        }
        finally
        {
            _contentRoot.RemoveFile(filePath);
        }

        var duration = audioResource.AudioStream?.Length ?? TimeSpan.Zero;
        var delay = duration + TimeSpan.FromSeconds(PlaybackDelay);

        Timer.Spawn(delay, () =>
        {
            onComplete?.Invoke();
        });
    }

    private void PlayRadioWithEffectInternal(AudioResource audioResource, ResolvedPathSpecifier soundSpecifier,
        AudioParams audioParams)
    {
        var audioResult = _audio.PlayGlobal(audioResource.AudioStream, soundSpecifier, audioParams);
        if (audioResult == null)
            return;

        ApplyRadioEffect(audioResult.Value);

        var secondParams = audioParams
            .WithPitchScale(audioParams.Pitch * (1 + _ran.NextFloat(0.02f, 0.05f)))
            .WithVolume(audioParams.Volume - 4f)
            .WithVariation(0.04f);

        _audio.PlayGlobal(audioResource.AudioStream, soundSpecifier, secondParams);
    }

    #region Utility Methods

    private float AdjustVolume(bool isGlobal, bool isWhisper, bool isRadio)
    {
        var volume = isRadio
            ? MinimalVolume + SharedAudioSystem.GainToVolume(_radioVolume)
            : MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isGlobal) return volume + SharedAudioSystem.GainToVolume(GlobalVolumeBonus);

        if (isWhisper)
        {
            var fade = isRadio ? WhisperFade * 0.15f : WhisperFade;
            volume -= SharedAudioSystem.GainToVolume(fade);
        }

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }

    private float GetRadioPitch()
    {
        var target = _ran.NextFloat(RadioPitchMin, RadioPitchMax);
        _lastRadioPitch = _lastRadioPitch * 0.7f + target * 0.3f;
        return _lastRadioPitch;
    }

    private float GetRadioVariation()
    {
        return _ran.NextFloat(RadioVariationMin, RadioVariationMax);
    }

    private float GetRadioRolloff()
    {
        return _ran.NextFloat(RadioRolloffMin, RadioRolloffMax);
    }

    #endregion
}

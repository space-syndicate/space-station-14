using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars; // Corvax-AdminMusic
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client.Audio;

/// <inheritdoc/>
public sealed partial class ClientGlobalSoundSystem : GlobalSoundSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    // Admin music
    private bool _adminAudioEnabled = true;
    private List<EntityUid?> _adminAudio = new(1);
    // Corvax-AdminMusic-Start
    private float _adminAudioVolume = 1f;
    private readonly Dictionary<EntityUid, float> _adminAudioBaseVolume = new();
    // Corvax-AdminMusic-End

    // Event sounds (e.g. nuke timer)
    private bool _eventAudioEnabled = true;
    private Dictionary<StationEventMusicType, EntityUid?> _eventAudio = new(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<AdminSoundEvent>(PlayAdminSound);
        Subs.CVar(_cfg, CCVars.AdminSoundsEnabled, ToggleAdminSound, true);
        Subs.CVar(_cfg, CCCVars.AdminMusicVolume, OnAdminAudioVolumeChanged, true); // Corvax-AdminMusic

        SubscribeNetworkEvent<StationEventMusicEvent>(PlayStationEventMusic);
        SubscribeNetworkEvent<StopStationEventMusic>(StopStationEventMusic);
        Subs.CVar(_cfg, CCVars.EventMusicEnabled, ToggleStationEventMusic, true);

        SubscribeNetworkEvent<GameGlobalSoundEvent>(PlayGameSound);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ClearAudio();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ClearAudio();
    }

    private void ClearAudio()
    {
        foreach (var stream in _adminAudio)
        {
            _audio.Stop(stream);
        }
        _adminAudio.Clear();
        _adminAudioBaseVolume.Clear(); // Corvax-AdminMusic

        foreach (var stream in _eventAudio.Values)
        {
            _audio.Stop(stream);
        }

        _eventAudio.Clear();
    }

    private void PlayAdminSound(AdminSoundEvent soundEvent)
    {
        if(!_adminAudioEnabled) return;

        // Corvax-AdminMusic-Start
        var baseParams = soundEvent.AudioParams ?? AudioParams.Default;
        var audioParams = baseParams.AddVolume(SharedAudioSystem.GainToVolume(_adminAudioVolume));
        var stream = _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, audioParams);
        if (stream != null)
            _adminAudioBaseVolume[stream.Value.Entity] = baseParams.Volume;
        // Corvax-AdminMusic-End
        _adminAudio.Add(stream?.Entity);
    }

    // Corvax-AdminMusic-Start
    private void OnAdminAudioVolumeChanged(float volume)
    {
        _adminAudioVolume = volume;

        foreach (var (uid, baseVolume) in _adminAudioBaseVolume)
        {
            if (Exists(uid))
                _audio.SetVolume(uid, baseVolume + SharedAudioSystem.GainToVolume(volume));
        }
    }
    // Corvax-AdminMusic-End

    private void PlayStationEventMusic(StationEventMusicEvent soundEvent)
    {
        // Either the cvar is disabled or it's already playing
        if(!_eventAudioEnabled || _eventAudio.ContainsKey(soundEvent.Type)) return;

        var stream = _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, soundEvent.AudioParams);
        _eventAudio.Add(soundEvent.Type, stream?.Entity);
    }

    private void PlayGameSound(GameGlobalSoundEvent soundEvent)
    {
        _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false, soundEvent.AudioParams);
    }

    private void StopStationEventMusic(StopStationEventMusic soundEvent)
    {
        if (!_eventAudio.TryGetValue(soundEvent.Type, out var stream))
            return;

        _audio.Stop(stream);
        _eventAudio.Remove(soundEvent.Type);
    }

    private void ToggleAdminSound(bool enabled)
    {
        _adminAudioEnabled = enabled;
        if (_adminAudioEnabled) return;
        foreach (var stream in _adminAudio)
        {
            _audio.Stop(stream);
        }
        _adminAudio.Clear();
        _adminAudioBaseVolume.Clear(); // Corvax-AdminMusic
    }

    private void ToggleStationEventMusic(bool enabled)
    {
        _eventAudioEnabled = enabled;
        if (_eventAudioEnabled) return;
        foreach (var stream in _eventAudio)
        {
            _audio.Stop(stream.Value);
        }
        _eventAudio.Clear();
    }
}

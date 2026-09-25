using System;
using System.Collections.Generic;
using Content.Shared.Corvax.Administration.MusicPlayer;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Client.Corvax.Administration.MusicPlayer;

public sealed class AdminMusicAudioSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly Dictionary<Guid, EntityUid> _localStreams = new Dictionary<Guid, EntityUid>();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AdminMusicClientPlayMessage>(HandlePlayMessage);
        SubscribeNetworkEvent<AdminMusicClientStopMessage>(HandleStopMessage);
    }

    private void HandlePlayMessage(AdminMusicClientPlayMessage msg)
    {
        if (_localStreams.TryGetValue(msg.SessionId, out var existingStream))
        {
            _audio.Stop(existingStream);
            _localStreams.Remove(msg.SessionId);
        }

        var audioParams = AudioParams.Default
            .WithVolume(msg.Volume)
            .WithLoop(false);
            
        var soundSpecifier = new SoundPathSpecifier(msg.TrackPath);
        
        var stream = _audio.PlayGlobal(soundSpecifier, Filter.Local(), false, audioParams);

        if (stream != null)
        {
            _localStreams[msg.SessionId] = stream.Value.Entity;
            
            _audio.SetPlaybackPosition(stream.Value.Entity, msg.TargetSeconds);
        }
    }

    private void HandleStopMessage(AdminMusicClientStopMessage msg)
    {
        ForceStopSession(msg.SessionId);
    }

    public void ForceStopSession(Guid sessionId)
    {
        if (_localStreams.TryGetValue(sessionId, out var stream))
        {
            _audio.Stop(stream);
            _localStreams.Remove(sessionId);
        }
    }
}

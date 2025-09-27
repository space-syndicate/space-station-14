using Content.Server.Administration.Managers;
using Content.Server.Audio.Jukebox;
using Content.Server.EUI;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Corvax.Audio;
using Content.Shared.Eui;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Corvax.Audio.UI;

public sealed class AudioControlsEui : BaseEui
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private readonly JukeboxSystem _jukeboxSystem;

    private readonly EntityUid _targetEntity;
    private AudioParams _targetAudioParams = AudioParams.Default;

    public AudioControlsEui(EntityUid entity)
    {
        IoCManager.InjectDependencies(this);
        _jukeboxSystem = _entityManager.System<JukeboxSystem>();
        _targetEntity = entity;
    }

    public override EuiStateBase GetNewState()
    {
        if (!_entityManager.TryGetNetEntity(_targetEntity, out var netEntity))
            throw new InvalidOperationException("Looks like targetEntity only present localy");

        return new AudioControlsEuiState()
        {
            TargetNetEntity = (NetEntity)netEntity,
            Pitch = _targetAudioParams.Pitch,
            Range = (int)_targetAudioParams.MaxDistance,
            Volume = _targetAudioParams.Volume,
        };
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        switch (msg)
        {
            case AudioControlsVolumeChangedMessage volumeMessage:
                _targetAudioParams.Volume = volumeMessage.Volume;
                _jukeboxSystem.SetAudioParams(_targetEntity, _targetAudioParams);
                break;
            case AudioControlsPitchChangedMessage pitchMessage:
                _targetAudioParams.Pitch = pitchMessage.Pitch;
                _jukeboxSystem.SetAudioParams(_targetEntity, _targetAudioParams);
                break;
            case AudioControlsRangeChangedMessage rangeMessage:
                _targetAudioParams.MaxDistance = rangeMessage.Range;
                _jukeboxSystem.SetAudioParams(_targetEntity, _targetAudioParams);
                break;
        }
        StateDirty();
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }
}

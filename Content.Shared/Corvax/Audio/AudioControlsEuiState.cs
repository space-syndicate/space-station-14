using Content.Shared.Eui;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Audio;

[Serializable, NetSerializable]
public sealed class AudioControlsEuiState : EuiStateBase
{
    public NetEntity TargetNetEntity;
    public float Volume;
    public int Range;
    public float Pitch;
}

[Serializable, NetSerializable]
public sealed class AudioControlsVolumeChangedMessage(float volume) : EuiMessageBase
{
    public float Volume = volume;
}

[Serializable, NetSerializable]
public sealed class AudioControlsPitchChangedMessage(float pitch) : EuiMessageBase
{
    public float Pitch = pitch;
}

[Serializable, NetSerializable]
public sealed class AudioControlsRangeChangedMessage(int range) : EuiMessageBase
{
    public int Range = range;
}


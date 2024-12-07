using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Administration.UI.Audio;

[Serializable, NetSerializable]
public sealed partial class AdminAudioPanelEuiState(bool playing, NetEntity audio, float volume, Queue<string> queue, bool global, Dictionary<Guid, string> players, HashSet<Guid> selectedPlayers) : EuiStateBase
{
    public bool Playing = playing;
    public NetEntity Audio = audio;
    public float Volume = volume;
    public Queue<string> Queue = queue;
    public bool Global = global;
    public Dictionary<Guid, string> Players = players;
    public HashSet<Guid> SelectedPlayers = selectedPlayers;
};

public static class AdminAudioPanelEuiMessage
{
    [Serializable]
    public abstract class AdminAudioPanelEuiMessageBase : EuiMessageBase
    {
    }

    [Serializable, NetSerializable]
    public sealed partial class Play : AdminAudioPanelEuiMessageBase
    {
    }

    [Serializable, NetSerializable]
    public sealed partial class Pause : AdminAudioPanelEuiMessageBase
    {
    }

    [Serializable, NetSerializable]
    public sealed partial class Stop : AdminAudioPanelEuiMessageBase
    {
    }

    [Serializable, NetSerializable]
    public sealed partial class AddTrack(string filename) : AdminAudioPanelEuiMessageBase
    {
        public string Filename = filename;
    }

    [Serializable, NetSerializable]
    public sealed partial class SetVolume(float volume) : AdminAudioPanelEuiMessageBase
    {
        public float Volume = volume;
    }

    [Serializable, NetSerializable]
    public sealed partial class SetPlaybackPosition(float position) : AdminAudioPanelEuiMessageBase
    {
        public float Position = position;
    }

    [Serializable, NetSerializable]
    public sealed partial class SelectPlayer(Guid player) : AdminAudioPanelEuiMessageBase
    {
        public Guid Player = player;
    }

    [Serializable, NetSerializable]
    public sealed partial class UnselectPlayer(Guid player) : AdminAudioPanelEuiMessageBase
    {
        public Guid Player = player;
    }

    [Serializable, NetSerializable]
    public sealed partial class GlobalToggled(bool toggled) : AdminAudioPanelEuiMessageBase
    {
        public bool Toggled = toggled;
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public sealed class PlayTTSEvent : EntityEventArgs
{
    public NetEntity Uid { get; }
    public byte[] Data { get; }
    public bool IsWhisper { get; }

    public PlayTTSEvent(NetEntity uid, byte[] data, bool isWhisper = false)
    {
        Uid = uid;
        Data = data;
        IsWhisper = isWhisper;
    }
}

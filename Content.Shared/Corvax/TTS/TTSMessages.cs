using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public sealed class PlayTTSMessage : EntityEventArgs
{
    public byte[] Data { get; }

    public PlayTTSMessage(byte[] data)
    {
        Data = data;
    }
}

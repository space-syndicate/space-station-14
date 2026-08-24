using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Cinema;

/// <summary>Client request for one chunk of a generated cinema audio segment.</summary>
[Serializable, NetSerializable]
public sealed class CinemaAudioChunkRequestEvent : EntityEventArgs
{
    public NetEntity Entity { get; }
    public string Key { get; }
    public int Segment { get; }
    public int ChunkIndex { get; }

    public CinemaAudioChunkRequestEvent(NetEntity entity, string key, int segment, int chunkIndex)
    {
        Entity = entity;
        Key = key;
        Segment = segment;
        ChunkIndex = chunkIndex;
    }
}

/// <summary>
/// Server response containing one OGG chunk. This deliberately uses the same serialized network-event path as TTS,
/// instead of defining another raw NetMessage ID.
/// </summary>
[Serializable, NetSerializable]
public sealed class CinemaAudioChunkEvent : EntityEventArgs
{
    public const int MaxDataBytes = 2 * 1024 * 1024;
    public const int MaxChunkBytes = 24 * 1024;

    public string Key { get; }
    public int Segment { get; }
    public int ChunkIndex { get; }
    public int ChunkCount { get; }
    public int TotalLength { get; }
    public byte[] Data { get; }

    public CinemaAudioChunkEvent(
        string key,
        int segment,
        int chunkIndex = 0,
        int chunkCount = 0,
        int totalLength = 0,
        byte[]? data = null)
    {
        Key = key;
        Segment = segment;
        ChunkIndex = chunkIndex;
        ChunkCount = chunkCount;
        TotalLength = totalLength;
        Data = data ?? [];
    }
}

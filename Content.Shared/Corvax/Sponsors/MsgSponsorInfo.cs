using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Corvax.Sponsors;


[Serializable, NetSerializable]
public sealed class SponsorInfo
{
    [JsonPropertyName("CharacterName")]
    public string CharacterName { get; set; } = null!;

    [JsonPropertyName("tier")]
    public int? Tier { get; set; }

    [JsonPropertyName("oocColor")]
    public string? OOCColor { get; set; }

    [JsonPropertyName("priorityJoin")]
    public bool HavePriorityJoin { get; set; } = false;

    [JsonPropertyName("extraSlots")]
    public int ExtraSlots { get; set; }

    [JsonPropertyName("allowedMarkings")]
    public string[] AllowedMarkings { get; set; } = Array.Empty<string>();
}

public sealed class MsgSponsorListInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public SponsorInfo[]? Sponsors;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var _listSponsors = new List<SponsorInfo>();
        var _itemsCount = buffer.ReadVariableInt32();
        buffer.ReadPadBits();
        for (int i = 0; i <= _itemsCount - 1; i++)
        {
            SponsorInfo _sponsor;
            var length = buffer.ReadVariableInt32();
            using var stream = buffer.ReadAlignedMemory(length);
            serializer.DeserializeDirect(stream, out _sponsor);
            _listSponsors.Add(_sponsor);
            buffer.ReadPadBits();
        }

        Sponsors = _listSponsors.ToArray();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        var _itemsCount = Sponsors?.Length ?? 0;
        buffer.WriteVariableInt32(_itemsCount);
        buffer.WritePadBits();
        if (Sponsors != null)
        {
            foreach (var sponsor in Sponsors)
            {
                var stream = new MemoryStream();
                serializer.SerializeDirect(stream, sponsor);
                buffer.WriteVariableInt32((int) stream.Length);
                buffer.Write(stream.AsSpan());
                buffer.WritePadBits();
            }
        }
    }
}


/// <summary>
/// Server sends sponsoring info to client on connect only if user is sponsor
/// </summary>
public sealed class MsgSponsorInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public SponsorInfo? Info;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var isSponsor = buffer.ReadBoolean();
        buffer.ReadPadBits();
        if (!isSponsor) return;
        var length = buffer.ReadVariableInt32();
        using var stream = buffer.ReadAlignedMemory(length);
        serializer.DeserializeDirect(stream, out Info);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Info != null);
        buffer.WritePadBits();
        if (Info == null) return;
        var stream = new MemoryStream();
        serializer.SerializeDirect(stream, Info);
        buffer.WriteVariableInt32((int) stream.Length);
        buffer.Write(stream.AsSpan());
    }
}

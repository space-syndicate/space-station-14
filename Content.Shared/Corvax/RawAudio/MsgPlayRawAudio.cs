using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.RawAudio;

public sealed class MsgPlayRawAudio : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public byte[] Data = {};
    
    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var length = buffer.ReadInt32();
        Data = buffer.ReadBytes(length);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Data.Length);
        buffer.Write(Data);
    }
}
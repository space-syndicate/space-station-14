using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Sponsors;

/// <summary>
/// Server sends sponsoring info to client on connect only if user is sponsor
/// </summary>
public sealed class MsgSponsoringInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool IsSponsor;
    public string[] AllowedMarkings = Array.Empty<string>();
    
    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        IsSponsor = buffer.ReadBoolean();
        buffer.ReadPadBits();

        var count = buffer.ReadVariableInt32();
        AllowedMarkings = new string[count];
        for (int i = 0; i < count; i++)
        {
            AllowedMarkings[i] = buffer.ReadString();
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(IsSponsor);
        buffer.WritePadBits();

        buffer.WriteVariableInt32(AllowedMarkings.Length);
        foreach (var markingId in AllowedMarkings)
        {
            buffer.Write(markingId);
        }
    }
}

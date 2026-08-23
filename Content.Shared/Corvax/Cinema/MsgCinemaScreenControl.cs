using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Cinema;

/// <summary>
/// Client → server request to change a cinema screen. The server re-validates the sender (admin only)
/// and the requested URL (https + whitelist) before applying anything.
/// </summary>
public sealed class MsgCinemaScreenControl : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public NetEntity Entity;
    public CinemaScreenAction Action;
    public string Url = string.Empty;
    public double SeekSeconds;
    public float Volume;

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableUnordered;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Entity = buffer.ReadNetEntity();
        Action = (CinemaScreenAction) buffer.ReadByte();
        Url = buffer.ReadString();
        SeekSeconds = buffer.ReadDouble();
        Volume = buffer.ReadFloat();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Entity);
        buffer.Write((byte) Action);
        buffer.Write(Url);
        buffer.Write(SeekSeconds);
        buffer.Write(Volume);
    }
}

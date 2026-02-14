// #SB AndreyCamper
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using System.Collections.Generic;
using Robust.Shared.GameObjects;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class RadarProjectileMessage : BoundUserInterfaceMessage
{
    public List<(NetCoordinates Coords, Angle Angle, byte Type)> Projectiles;
    public List<(NetCoordinates Coords, Angle Angle, float Length, byte Type)> Lasers;

    public RadarProjectileMessage(List<(NetCoordinates Coords, Angle Angle, byte Type)> projectiles,
    List<(NetCoordinates Coords, Angle Angle, float Length, byte Type)> lasers)
    {
        Projectiles = projectiles;
        Lasers = lasers;
    }
}

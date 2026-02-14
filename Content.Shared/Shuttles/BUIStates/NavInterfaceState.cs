using Robust.Shared.Map;
using Robust.Shared.Serialization;
using System.Collections.Generic; // #SB AndreyCamper
using Robust.Shared.Maths; // #SB AndreyCamper

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class NavInterfaceState
{
    public float MaxRange;

    /// <summary>
    /// The relevant coordinates to base the radar around.
    /// </summary>
    public NetCoordinates? Coordinates;

    /// <summary>
    /// The relevant rotation to rotate the angle around.
    /// </summary>
    public Angle? Angle;

    public Dictionary<NetEntity, List<DockingPortState>> Docks;

    public bool RotateWithEntity = true;

	public List<(NetCoordinates, Angle, byte)> Projectiles; // #SB AndreyCamper
    public List<(NetCoordinates, Angle, float, byte)> Lasers;     // #SB AndreyCamper

    public NavInterfaceState(
        float maxRange,
        NetCoordinates? coordinates,
        Angle? angle,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        List<(NetCoordinates, Angle, byte)> projectiles, // #SB AndreyCamper
        List<(NetCoordinates, Angle, float, byte)> lasers) // #SB AndreyCamper
    {
        MaxRange = maxRange;
        Coordinates = coordinates;
        Angle = angle;
        Docks = docks;
        Projectiles = projectiles; // #SB AndreyCamper
        Lasers = lasers; // #SB AndreyCamper
    }
}

[Serializable, NetSerializable]
public enum RadarConsoleUiKey : byte
{
    Key
}

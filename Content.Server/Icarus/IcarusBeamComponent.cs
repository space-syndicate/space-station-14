using Content.Shared.Sound;

namespace Content.Server.Icarus;

[RegisterComponent]
public sealed class IcarusBeamComponent : Component
{
    [DataField("loopSound")]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Corvax/AssaultOperatives/sunbeam_loop.ogg");

    [DataField("speed")]
    public float Speed = 1f;
}

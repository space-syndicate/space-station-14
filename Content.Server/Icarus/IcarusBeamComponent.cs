using Content.Shared.Sound;

namespace Content.Server.Icarus;

[RegisterComponent]
public sealed class IcarusBeamComponent : Component
{
    [DataField("loopSound")]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Corvax/AssaultOperatives/sunbeam_loop.ogg");

    /// <summary>
    ///     Beam moving speed.
    /// </summary>
    [DataField("speed")]
    public float Speed = 1f;

    /// <summary>
    ///     The beam will be automatically cleaned up after this time.
    /// </summary>
    [DataField("lifetime")]
    public TimeSpan Lifetime = TimeSpan.FromSeconds(240);

    /// <summary>
    ///     With this set to true, beam will automatically set the tiles under them to space.
    /// </summary>
    [DataField("destroyTiles")]
    public bool DestroyTiles = false;

    [DataField("destroyRadius")]
    public float DestroyRadius = 2f;

    [DataField("accumulator")]
    public float Accumulator = 0f;
}

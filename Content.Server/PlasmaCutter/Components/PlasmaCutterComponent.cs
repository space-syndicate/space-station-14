using System.Threading;
using Robust.Shared.Audio;
using Content.Shared.Damage;

namespace Content.Server.PlasmaCutter.Components;

[RegisterComponent]
public sealed class PlasmaCutterComponent : Component
{
    private const int DefaultAmmoCount = 10;

    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("maxAmmo")] public int MaxAmmo = DefaultAmmoCount;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("delay")]
    public float Delay = 2f;

    [DataField("sparksSound")]
    public SoundSpecifier SparksSound { get; set; } = new SoundCollectionSpecifier("sparks");

    [DataField("workSound")]
    public SoundSpecifier workSound { get; set; } = new SoundCollectionSpecifier("welder");

    [DataField("swapModeSound")]
    public SoundSpecifier SwapModeSound = new SoundPathSpecifier("/Audio/Items/genhit.ogg");

    [DataField("successSound")]
    public SoundSpecifier successSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");

    [DataField("activatedMeleeDamageBonus")]
    public DamageSpecifier ActivatedMeleeDamageBonus = new();

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("ammo")]
    public int CurrentAmmo = DefaultAmmoCount;

    public bool Activated = false;

    public CancellationTokenSource? CancelToken = null;
}

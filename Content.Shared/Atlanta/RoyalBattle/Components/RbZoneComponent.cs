using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Atlanta.RoyalBattle.Components;

/// <summary>
/// Makes zone over the grid. Uses to take damage to entities which not in range.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RbZoneComponent : Component
{
    [DataField("enabled")]
    public bool IsEnabled = false;

    [DataField("damage", required: true)]
    public DamageSpecifier? Damage = new();

    [DataField("damageMultiplier")]
    public float DamageMultiplier = 1f;

    [DataField("range", required: true), AutoNetworkedField]
    public float Range;

    [DataField("rangeLerp"), AutoNetworkedField]
    public float RangeLerp = 100f;

    [DataField("isMoving"), AutoNetworkedField]
    public bool IsMoving = false;

    [DataField("rangeMultiplier")]
    public float RangeMultiplier = 0.5f;

    [DataField("rangeMultiplierRatio")]
    public float RangeRatio = 1f;

    [DataField("waveTiming")]
    public TimeSpan WaveTiming = TimeSpan.FromMinutes(2);

    [DataField("nextWave"), AutoNetworkedField]
    public TimeSpan NextWave;

    [DataField("waveTimingMultiplier")]
    public float WaveTimingMultiplier = 0.85f;

    [DataField("zoneSpeed"), AutoNetworkedField]
    public float ZoneSpeed = 1f;

    [DataField("center"), AutoNetworkedField]
    public Vector2 Center = Vector2.Zero;

    [DataField("wavesCount")]
    public int WavesCount = 5;

    [DataField("lastDamageTime")]
    public TimeSpan LastDamageTime;

    [DataField("damageTiming")]
    public TimeSpan DamageTiming = TimeSpan.FromSeconds(1);

    public SpriteSpecifier ZoneTexture = new SpriteSpecifier.Texture(
        new ResPath("Effects/fire.rsi/zone.png"));
}

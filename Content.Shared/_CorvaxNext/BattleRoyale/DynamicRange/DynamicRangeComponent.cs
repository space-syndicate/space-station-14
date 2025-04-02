using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared._CorvaxNext.BattleRoyale.DynamicRange;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DynamicRangeComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float Range = 200f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Vector2 Origin;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsShrinking = true;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float ShrinkTime = 730f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinimumRange = 10f;
    
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinOriginX = -60f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxOriginX = 120f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinOriginY = -50f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxOriginY = 120f;
    
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DamageInterval = 1.0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float OutOfBoundsDamage = 10.0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string DamageType = "Asphyxiation";
    
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SearchRangeMultiplier = 1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinSearchRange = 300;
    
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool Processed;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool OriginInitialized;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float? InitialRange;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? ShrinkStartTime;

    [DataField("lastDamageTimes"), ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<EntityUid, TimeSpan> LastDamageTimes = new();
    
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float PreviousRange;
    
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public Vector2 PreviousOrigin;
    
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool PreviousShrinking;
    
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float PreviousShrinkTime;
    
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float PreviousMinRange;
    
    [DataField]
    public SoundSpecifier ShrinkMusic = new SoundCollectionSpecifier("BattleRoyale");
    
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool PlayedShrinkMusic = false;
    
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MusicStartTime = 600f;
}

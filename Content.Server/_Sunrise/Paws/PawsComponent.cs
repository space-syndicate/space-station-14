using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
namespace Content.Server.Sunrise.Paws
{
    [RegisterComponent]
    public sealed partial class PawsComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("screamInterval")]
        public float ScreamInterval = 3;
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("coughInterval")]
        public float CoughInterval = 5;
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("thresholdDamage")]
        public FixedPoint2 ThresholdDamage = 5;
        public List<string> EmotesTakeDamage = new()
        {
            "Scream",
            "Crying"
        };
        [DataField("nextChargeTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan NextScreamTime = TimeSpan.FromSeconds(0);
        [DataField("nextCoughTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan NextCoughTime = TimeSpan.FromSeconds(0);
    }
}
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
namespace Content.Server.Medical.Components
{
    [RegisterComponent]
    public sealed partial class SurgeryToolComponent : Component
    {
        [DataField("damage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier Damage = default!;
        [DataField("damageContainers", customTypeSerializer: typeof(PrototypeIdListSerializer<DamageContainerPrototype>))]
        public List<string>? DamageContainers;
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("delay")]
        public float Delay = 1f;
        [DataField("InProgressSound")]
        public SoundSpecifier? InProgressSound = null;
        [DataField("CauterySound")]
        public SoundSpecifier? CauterySound = null;
        [DataField("ScalpelSound")]
        public SoundSpecifier? ScalpelSound = null;
        [DataField("IsCautery")]
        public bool? IsCautery = null;
        [DataField("IsScalpel")]
        public bool? IsScalpel = null;
        [DataField("IsHemostat")]
        public bool? IsHemostat = null;
    }
}

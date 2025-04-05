using Content.Shared.Roles;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._CorvaxNext.BattleRoyale.Rules.Components;

[RegisterComponent, Access(typeof(BattleRoyaleRuleSystem))]
public sealed partial class BattleRoyaleRuleComponent : Component
{
    [DataField("gear", customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string Gear = "BattleRoyaleGear";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RoundEndDelay = TimeSpan.FromSeconds(10f);

    [DataField]
    public EntityUid? Victor;
	
	[DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool WinnerAnnounced = false;
}

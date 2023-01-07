using Content.Server.GameTicking.Rules.Configurations;
using Content.Shared.Dataset;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.AssaultOps;

public sealed class AssaultopsRuleConfiguration : GameRuleConfiguration
{
    public override string Id => "Assaultops";

    [DataField("minPlayers")]
    public int MinPlayers = 20;
    
    /// <summary>
    ///     This INCLUDES the operatives. So a value of 3 is satisfied by 2 players & 1 operative
    /// </summary>
    [DataField("playersPerOperative")]
    public int PlayersPerOperative = 5;

    [DataField("maxOps")]
    public int MaxOperatives = 5;
    
    [DataField("requiredKeys")]
    public int RequiredKeys = 3;

    [DataField("keysCarrierJobs", customTypeSerializer: typeof(PrototypeIdArraySerializer<JobPrototype>))]
    public string[] KeysCarrierJobs = { "Captain", "HeadOfSecurity", "ChiefEngineer", "ChiefMedicalOfficer", "ResearchDirector", "Quartermaster" };

    [DataField("randomHumanoidSettings", customTypeSerializer: typeof(PrototypeIdSerializer<RandomHumanoidSettingsPrototype>))]
    public string RandomHumanoidSettingsPrototype = "AssaultOp";

    [DataField("spawnPointProto", customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>))]
    public string SpawnPointPrototype = "SpawnPointAssaultops";

    [DataField("operativeRoleProto", customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>))]
    public string OperativeRoleProto = "Assaultops";

    [DataField("operativeStartGearProto", customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>))]
    public string OperativeStartGearPrototype = "SyndicateOperativeGearFull"; // TODO: Use stealth gear

    [DataField("normalNames", customTypeSerializer: typeof(PrototypeIdSerializer<DatasetPrototype>))]
    public string OperativeNames = "SyndicateNamesNormal";

    [DataField("outpostMap", customTypeSerializer: typeof(ResourcePathSerializer))]
    public ResourcePath? OutpostMap = new("/Maps/nukieplanet.yml"); // TODO: Create some new map

    [DataField("shuttleMap", customTypeSerializer: typeof(ResourcePathSerializer))]
    public ResourcePath? ShuttleMap = new("/Maps/infiltrator.yml"); // TODO: Create custom shuttle

    [DataField("greetingSound", customTypeSerializer: typeof(SoundSpecifierTypeSerializer))]
    public SoundSpecifier? GreetSound = new SoundPathSpecifier("/Audio/Corvax/Misc/assaultops.ogg");
}

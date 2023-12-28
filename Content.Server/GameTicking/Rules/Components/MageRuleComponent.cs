using Content.Server.NPC.Components;
using Content.Server.RoundEnd;
using Content.Server.StationEvents.Events;
using Content.Shared.Dataset;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;


namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(MageRuleSystem))]
public sealed partial class MageRuleComponent : Component
{
    // TODO Replace with GameRuleComponent.minPlayers
    /// <summary>
    /// The minimum needed amount of players
    /// </summary>
    [DataField]
    public int MinPlayers = 30;

    /// <summary>
    ///     This INCLUDES the operatives. So a value of 3 is satisfied by 2 players & 1 operative
    /// </summary>
    [DataField]
    public int PlayersPerMage = 20;

    [DataField]
    public int MaxMage = 5;

    [DataField]
    public TimeSpan EvacShuttleTime = TimeSpan.FromMinutes(3);
    /// <summary>
    /// What will happen if all of the nuclear operatives will die. Used by LoneOpsSpawn event.
    /// </summary>
    [DataField]
    public RoundEndBehavior RoundEndBehavior = RoundEndBehavior.ShuttleCall;

    /// <summary>
    /// Text for shuttle call if RoundEndBehavior is ShuttleCall.
    /// </summary>
    [DataField]
    public string RoundEndTextSender = "comms-console-announcement-title-centcom";

    /// <summary>
    /// Text for shuttle call if RoundEndBehavior is ShuttleCall.
    /// </summary>
    [DataField]
    public string RoundEndTextShuttleCall = "mage-no-more-threat-announcement-shuttle-call";

    /// <summary>
    /// Text for announcement if RoundEndBehavior is ShuttleCall. Used if shuttle is already called
    /// </summary>
    [DataField]
    public string RoundEndTextAnnouncement = "mage-no-more-threat-announcement";

    [DataField]
    public EntProtoId SpawnPointProto = "SpawnPointMage";

    [DataField]
    public ProtoId<AntagPrototype> MageRoleProto = "Mage";

    [DataField]
    public EntityUid Shuttle;

    [DataField]
    public EntProtoId GhostSpawnPointProto = "SpawnPointGhostNukeMage";
    [DataField]
    public ProtoId<StartingGearPrototype> MageStartGearProto = "WizardBlueGear";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<DatasetPrototype>))]
    public string FirstNames = "names_wizard_first";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<DatasetPrototype>))]
    public string LastNames = "names_wizard_last";

    [DataField(customTypeSerializer: typeof(ResPathSerializer))]
    public ResPath ShuttleMap = new("/Maps/Shuttles/wizard.yml");

    [DataField]
    public WinType Win = WinType.MageWin;

    [DataField]
    public List<WinCondition> WinConditions = new();

    /// <summary>
    ///     Cached starting gear prototypes.
    /// </summary>
    [DataField]
    public Dictionary<string, StartingGearPrototype> StartingGearPrototypes = new();

    /// <summary>
    ///     Cached operator name prototypes.
    /// </summary>
    [DataField]
    public Dictionary<string, List<string>> MageNames = new();

    /// <summary>
    ///     Data to be used in <see cref="OnMindAdded"/> for an operative once the Mind has been added.
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, string> MageMindPendingData = new();

    /// <summary>
    ///     Players who played as an operative at some point in the round.
    ///     Stores the mind as well as the entity name
    /// </summary>
    [DataField]
    public Dictionary<string, EntityUid> MagePlayers = new();

    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction = default!;
}

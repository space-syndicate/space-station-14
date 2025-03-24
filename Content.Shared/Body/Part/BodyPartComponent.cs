using Content.Shared._CorvaxNext.Surgery.Tools;
using Content.Shared._CorvaxNext.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Part;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//[Access(typeof(SharedBodySystem))]
public sealed partial class BodyPartComponent : Component, ISurgeryToolComponent
{
    // Need to set this on container changes as it may be several transform parents up the hierarchy.
    /// <summary>
    /// Parent body for this part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    // start-_CorvaxNext: surgery
    [DataField, AutoNetworkedField]
    public BodyPartSlot? ParentSlot;
    // end-_CorvaxNext: surgery

    // start-_CorvaxNext: surgery
    /// <summary>
    /// Shitmed Change: Bleeding stacks to give when this body part is severed.
    /// Doubled for <see cref="IsVital"/>. parts.
    /// </summary>
    [DataField]
    public float SeverBleeding = 4f;

    // start-_CorvaxNext: surgery
    [DataField, AlwaysPushInheritance]
    public string ToolName { get; set; } = "A body part";

    [DataField, AlwaysPushInheritance]
    public string SlotId { get; set; } = "";

    [DataField, AutoNetworkedField]
    public bool? Used { get; set; } = null;

    [DataField, AlwaysPushInheritance]
    public float Speed { get; set; } = 1f;

    /// <summary>
    /// CorvaxNext Change: What's the max health this body part can have?
    /// </summary>
    [DataField]
    public float MinIntegrity;

    /// <summary>
    /// Whether this body part can be severed or not
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanSever = true;

    /// <summary>
    ///     CorvaxNext Change: Whether this body part is enabled or not.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    ///     CorvaxNext Change: Whether this body part can be enabled or not. Used for non-functional prosthetics.
    /// </summary>
    [DataField]
    public bool CanEnable = true;

    /// <summary>
    /// Whether this body part can attach children or not.
    /// </summary>
    [DataField]
    public bool CanAttachChildren = true;

    /// <summary>
    ///     CorvaxNext Change: How long it takes to run another self heal tick on the body part.
    /// </summary>
    [DataField]
    public float HealingTime = 30;

    /// <summary>
    ///     CorvaxNext Change: How long it has been since the last self heal tick on the body part.
    /// </summary>
    public float HealingTimer;

    /// <summary>
    ///     CorvaxNext Change: How much health to heal on the body part per tick.
    /// </summary>
    [DataField]
    public float SelfHealingAmount = 5;

    /// <summary>
    ///     CorvaxNext Change: The name of the container for this body part. Used in insertion surgeries.
    /// </summary>
    [DataField]
    public string ContainerName { get; set; } = "part_slot";

    /// <summary>
    ///     CorvaxNext Change: The slot for item insertion.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ItemSlot ItemInsertionSlot = new();


    /// <summary>
    ///     CorvaxNext Change: Current species. Dictates things like body part sprites.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Species { get; set; } = "";

    /// <summary>
    ///     CorvaxNext Change: The total damage that has to be dealt to a body part
    ///     to make possible severing it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SeverIntegrity = 90;

    /// <summary>
    ///     CorvaxNext Change: The ID of the base layer for this body part.
    /// </summary>
    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public string? BaseLayerId;

    /// <summary>
    ///     CorvaxNext Change: On what TargetIntegrity we should re-enable the part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TargetIntegrity EnableIntegrity = TargetIntegrity.ModeratelyWounded;

    [DataField, AutoNetworkedField]
    public Dictionary<TargetIntegrity, float> IntegrityThresholds = new()
    {
        { TargetIntegrity.CriticallyWounded, 75 },
        { TargetIntegrity.HeavilyWounded, 60 },
        { TargetIntegrity.ModeratelyWounded, 50 },
        { TargetIntegrity.SomewhatWounded, 35 },
        { TargetIntegrity.LightlyWounded, 20 },
        { TargetIntegrity.Healthy, 10 },
    };

    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public BodyPartType PartType = BodyPartType.Other;


    // TODO BODY Replace with a simulation of organs
    /// <summary>
    ///     Whether or not the owning <see cref="Body"/> will die if all
    ///     <see cref="BodyComponent"/>s of this type are removed from it.
    /// </summary>
    [DataField("vital"), AutoNetworkedField]
    public bool IsVital;

    [DataField, AutoNetworkedField]
    public BodyPartSymmetry Symmetry { get; set; } = BodyPartSymmetry.None;

    /// <summary>
    ///     When attached, the part will ensure these components on the entity, and delete them on removal.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnAdd;

    /// <summary>
    ///     When removed, the part will ensure these components on the entity, and add them on removal.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnRemove;

    /// <summary>
    /// Child body parts attached to this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, BodyPartSlot> Children = new();

    /// <summary>
    /// Organs attached to this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, OrganSlot> Organs = new();

    // end-_CorvaxNext: surgery Change End

    /// <summary>
    /// These are only for VV/Debug do not use these for gameplay/systems
    /// </summary>
    [ViewVariables]
    private List<ContainerSlot> BodyPartSlotsVV
    {
        get
        {
            List<ContainerSlot> temp = new();
            var containerSystem = IoCManager.Resolve<IEntityManager>().System<SharedContainerSystem>();

            foreach (var slotId in Children.Keys)
            {
                temp.Add((ContainerSlot)containerSystem.GetContainer(Owner, SharedBodySystem.PartSlotContainerIdPrefix + slotId));
            }

            return temp;
        }
    }

    [ViewVariables]
    private List<ContainerSlot> OrganSlotsVV
    {
        get
        {
            List<ContainerSlot> temp = new();
            var containerSystem = IoCManager.Resolve<IEntityManager>().System<SharedContainerSystem>();

            foreach (var slotId in Organs.Keys)
            {
                temp.Add((ContainerSlot)containerSystem.GetContainer(Owner, SharedBodySystem.OrganSlotContainerIdPrefix + slotId));
            }

            return temp;
        }
    }
}

/// <summary>
/// Contains metadata about a body part in relation to its slot.
/// </summary>
[NetSerializable, Serializable]
[DataRecord]
public partial struct BodyPartSlot
{
    public string Id;
    public BodyPartType Type;

    public BodyPartSlot(string id, BodyPartType type)
    {
        Id = id;
        Type = type;
    }
};

/// <summary>
/// Contains metadata about an organ part in relation to its slot.
/// </summary>
[NetSerializable, Serializable]
[DataRecord]
public partial struct OrganSlot
{
    public string Id;

    public OrganSlot(string id)
    {
        Id = id;
    }
};

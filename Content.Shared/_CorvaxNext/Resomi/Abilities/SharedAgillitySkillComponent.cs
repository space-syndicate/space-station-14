using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._CorvaxNext.Resomi.Abilities;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class AgillitySkillComponent : Component
{
    [AutoNetworkedField, DataField]
    public Dictionary<string, int> DisabledJumpUpFixtureMasks = new();
    [AutoNetworkedField, DataField]
    public Dictionary<string, int> DisabledJumpDownFixtureMasks = new();

    [DataField("active")]
    public bool Active = false;

    /// <summary>
    /// if we want the ability to not give the opportunity to jump on the tables and only accelerate
    /// </summary>
    [DataField("jumpEnabled")]
    public bool JumpEnabled = true;

    [DataField("switchAgilityAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? SwitchAgilityAction = "SwitchAgilityAction";

    [DataField("switchAgilityActionEntity")] public EntityUid? SwitchAgilityActionEntity;

    /// <summary>
    /// how much stamina will be spent for each jump
    /// </summary>
    [DataField("staminaDamageOnJump")]
    public float StaminaDamageOnJump = 10f;

    /// <summary>
    /// how much stamina will be passive spent while abilitty is activated
    /// </summary>
    [DataField("staminaDamagePassive")]
    public float StaminaDamagePassive = 3f;

    [DataField("sprintSpeedModifier")]
    public float SprintSpeedModifier = 0.1f; //+10%
    public float SprintSpeedCurrent = 1f;

    /// <summary>
    /// once in how many seconds is our stamina taken away while the ability is on
    /// </summary>
    [DataField("delay")]
    public double Delay = 1.0;
    public TimeSpan UpdateRate => TimeSpan.FromSeconds(Delay);
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// cooldown of ability. Called when the ability is disabled
    /// </summary>
    [DataField("cooldown")]
    public double Cooldown = 20.0;
    public TimeSpan CooldownDelay => TimeSpan.FromSeconds(Cooldown);
}

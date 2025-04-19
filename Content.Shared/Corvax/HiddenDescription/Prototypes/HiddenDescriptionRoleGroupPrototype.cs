using Content.Shared.Roles; // Needed for JobPrototype
using Robust.Shared.Prototypes; // Needed for IPrototype, PrototypeAttribute, IdDataField, DataFieldAttribute

// --- ИЗМЕНЕНО ПРОСТРАНСТВО ИМЕН ---
namespace Content.Shared.Corvax.HiddenDescription.Prototypes // Changed from Content.Server...
{
    /// <summary>
    /// Defines a reusable group of job roles (<see cref="JobPrototype"/>)
    /// for checking hidden description visibility.
    /// Defined in Shared to be accessible by the YAMLLinter.
    /// </summary>
    [Prototype("hiddenDescriptionRoleGroup")]
    public sealed class HiddenDescriptionRoleGroupPrototype : IPrototype
    {
        /// <summary>
        /// The unique identifier for this prototype. Referenced by <see cref="HiddenDescriptionEntry.RoleGroupRequired"/>.
        /// </summary>
        [IdDataField]
        public string ID { get; private set; } = default!;

        /// <summary>
        /// List of <see cref="JobPrototype"/> IDs included in this group.
        /// </summary>
        [DataField("jobs", required: true)]
        public List<ProtoId<JobPrototype>> Jobs { get; private set; } = new();
    }
}

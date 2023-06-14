using Content.Server.Humanoid;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.Speech.Components;

/// <summary>
///     Component required for entities to be able to do vocal emotions.
/// </summary>
[RegisterComponent]
[Access(typeof(SnapSystem))]
public sealed class SnapComponent : Component
{
    /// <summary>
    ///     Emote sounds prototype id for each sex (not gender).
    ///     Entities without <see cref="HumanoidComponent"/> considered to be <see cref="Sex.Unsexed"/>.
    /// </summary>
    [DataField("sounds", customTypeSerializer: typeof(PrototypeIdValueDictionarySerializer<Sex, EmoteSoundsPrototype>))]
    public Dictionary<Sex, string>? Sounds;

    [DataField("SnapId", customTypeSerializer: typeof(PrototypeIdSerializer<EmotePrototype>))]
    public string SnapId = "Snap";

    [DataField("Snap")]
    public SoundSpecifier Snap = new SoundPathSpecifier("/Audio/Voice/Human/manSnap1.ogg");

    [DataField("SnapProbability")]
    public float SnapProbability = 0.0002f;

    [DataField("SnapActionId", customTypeSerializer: typeof(PrototypeIdSerializer<InstantActionPrototype>))]
    public string SnapActionId = "Snap";

    [DataField("SnapAction")]
    public InstantAction? SnapAction;

    /// <summary>
    ///     Currently loaded emote sounds prototype, based on entity sex.
    ///     Null if no valid prototype for entity sex was found.
    /// </summary>
    [ViewVariables]
    public EmoteSoundsPrototype? EmoteSounds = null;
}

public sealed class SnapActionEvent : InstantActionEvent
{

}

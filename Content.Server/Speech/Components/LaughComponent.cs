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
[Access(typeof(LaughSystem))]
public sealed class LaughComponent : Component
{
    /// <summary>
    ///     Emote sounds prototype id for each sex (not gender).
    ///     Entities without <see cref="HumanoidComponent"/> considered to be <see cref="Sex.Unsexed"/>.
    /// </summary>
    [DataField("sounds", customTypeSerializer: typeof(PrototypeIdValueDictionarySerializer<Sex, EmoteSoundsPrototype>))]
    public Dictionary<Sex, string>? Sounds;

    [DataField("LaughId", customTypeSerializer: typeof(PrototypeIdSerializer<EmotePrototype>))]
    public string LaughId = "Laugh";

    [DataField("Laugh")]
    public SoundSpecifier Laugh = new SoundPathSpecifier("/Audio/Voice/Human/manlaugh1.ogg");

    [DataField("LaughProbability")]
    public float LaughProbability = 0.0002f;

    [DataField("LaughActionId", customTypeSerializer: typeof(PrototypeIdSerializer<InstantActionPrototype>))]
    public string LaughActionId = "Laugh";

    [DataField("LaughAction")]
    public InstantAction? LaughAction;

    /// <summary>
    ///     Currently loaded emote sounds prototype, based on entity sex.
    ///     Null if no valid prototype for entity sex was found.
    /// </summary>
    [ViewVariables]
    public EmoteSoundsPrototype? EmoteSounds = null;
}

public sealed class LaughActionEvent : InstantActionEvent
{

}

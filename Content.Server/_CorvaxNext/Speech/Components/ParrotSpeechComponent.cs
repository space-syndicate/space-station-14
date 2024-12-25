using Content.Shared.Whitelist;
using Content.Server.Speech.EntitySystems;

namespace Content.Server.Speech.Components;

/// <summary>
///     This component stores the parrot's learned phrases (both single words and multi-word phrases),
///     and also controls time intervals and learning probabilities.
/// </summary>
[RegisterComponent]
[Access(typeof(ParrotSpeechSystem))]
public sealed partial class ParrotSpeechComponent : Component
{
    /// <summary>
    ///     The maximum number of words in a generated phrase if the parrot decides to combine single words.
    /// </summary>
    [DataField]
    public int MaximumPhraseLength = 7;

    /// <summary>
    ///     The maximum amount of single-word phrases the parrot can store.
    /// </summary>
    [DataField]
    public int MaximumSingleWordCount = 60;

    /// <summary>
    ///     The maximum amount of multi-word phrases the parrot can store.
    /// </summary>
    [DataField]
    public int MaximumMultiWordCount = 20;

    /// <summary>
    ///     Minimum delay (in seconds) before the next utterance.
    /// </summary>
    [DataField]
    public int MinimumWait = 60; // 1 minute

    /// <summary>
    ///     Maximum delay (in seconds) before the next utterance.
    /// </summary>
    [DataField]
    public int MaximumWait = 120; // 2 minutes

    /// <summary>
    ///     Probability that the parrot learns an overheard phrase.
    /// </summary>
    [DataField]
    public float LearnChance = 0.2f;

    /// <summary>
    ///     List of entities that are blacklisted from parrot listening.
    ///     If the entity is in the blacklist, the parrot won't learn from them.
    /// </summary>
    [DataField]
    public EntityWhitelist Blacklist { get; private set; } = new();

    /// <summary>
    ///     Set of single-word phrases (unique words) the parrot has learned.
    /// </summary>
    [DataField(readOnly: true)]
    public HashSet<string> SingleWordPhrases = new();

    /// <summary>
    ///     Set of multi-word phrases (2 or more words) the parrot has learned.
    /// </summary>
    [DataField(readOnly: true)]
    public HashSet<string> MultiWordPhrases = new();

    /// <summary>
    ///     The next time the parrot will speak (when the current time is beyond this value).
    /// </summary>
    [DataField]
    public TimeSpan? NextUtterance;
}

using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Speech.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Speech.EntitySystems;

/// <summary>
///     This system handles the learning (when the parrot hears a phrase) and
///     the random utterances (when the parrot speaks).
/// </summary>
public sealed class ParrotSpeechSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ParrotSpeechComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<ParrotSpeechComponent, ListenAttemptEvent>(OnListenAttempt);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ParrotSpeechComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            // If the parrot has not learned anything, skip
            if (component.SingleWordPhrases.Count == 0 && component.MultiWordPhrases.Count == 0)
                continue;

            // If parrot is controlled by a player (has Mind), skip
            if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
                continue;

            // Check the time to speak
            if (_timing.CurTime < component.NextUtterance)
                continue;

            // Construct a phrase the parrot will say
            var phrase = PickRandomPhrase(component);
            if (string.IsNullOrWhiteSpace(phrase))
                continue;

            // Send the phrase to the chat system (hidden from chat/log to avoid spam)
            _chat.TrySendInGameICMessage(uid, phrase, InGameICChatType.Speak,
                hideChat: true,
                hideLog: true,
                checkRadioPrefix: false);

            // Reset next utterance time
            component.NextUtterance = _timing.CurTime +
                TimeSpan.FromSeconds(_random.Next(component.MinimumWait, component.MaximumWait));
        }
    }

    /// <summary>
    ///     Picks a random phrase to utter. May be a single word, a multi-word phrase, or
    ///     a combination of single words (up to MaximumPhraseLength).
    /// </summary>
    private string PickRandomPhrase(ParrotSpeechComponent component)
    {
        var singleCount = component.SingleWordPhrases.Count;
        var multiCount = component.MultiWordPhrases.Count;
        if (singleCount == 0 && multiCount == 0)
            return string.Empty;

        // 1) If we only have single words, use single approach
        // 2) If we only have multi-word phrases, use multi approach
        // 3) Otherwise, pick randomly among:
        //    a) Single word
        //    b) Full multi-word phrase
        //    c) Combined single words

        bool haveSingle = singleCount > 0;
        bool haveMulti = multiCount > 0;

        if (haveSingle && !haveMulti)
        {
            // Only single words exist
            return PickSingleWordOrCombine(component);
        }
        else if (!haveSingle && haveMulti)
        {
            // Only multi-word phrases exist
            return PickRandomMultiWord(component);
        }
        else
        {
            // We have both single and multi, choose approach
            var roll = _random.Next(3); // 0..2
            switch (roll)
            {
                case 0:
                    // single word
                    return PickSingleWord(component);
                case 1:
                    // multi-word phrase
                    return PickRandomMultiWord(component);
                default:
                    // combined single words
                    return CombineMultipleWords(component);
            }
        }
    }

    /// <summary>
    ///     If we only have single words, we can either speak a single one or combine them.
    /// </summary>
    private string PickSingleWordOrCombine(ParrotSpeechComponent component)
    {
        // 50% chance single word, 50% chance combine
        if (_random.Prob(0.5f))
        {
            return PickSingleWord(component);
        }
        else
        {
            return CombineMultipleWords(component);
        }
    }

    /// <summary>
    ///     Picks a random single word from SingleWordPhrases.
    /// </summary>
    private string PickSingleWord(ParrotSpeechComponent component)
    {
        var list = component.SingleWordPhrases.ToList();
        return _random.Pick(list);
    }

    /// <summary>
    ///     Picks a random multi-word phrase from MultiWordPhrases.
    /// </summary>
    private string PickRandomMultiWord(ParrotSpeechComponent component)
    {
        var list = component.MultiWordPhrases.ToList();
        return _random.Pick(list);
    }

    /// <summary>
    ///     Combines multiple single words (up to MaximumPhraseLength) into one phrase.
    ///     The length is random from 1 to max, but not exceeding the total single words we have.
    /// </summary>
    private string CombineMultipleWords(ParrotSpeechComponent component)
    {
        var countAvailable = component.SingleWordPhrases.Count;
        if (countAvailable == 0)
            return string.Empty;

        var maxCount = Math.Min(countAvailable, component.MaximumPhraseLength);
        var wordsToUse = _random.Next(1, maxCount + 1);

        var list = component.SingleWordPhrases.ToList();
        _random.Shuffle(list);

        var shuffled = list.Take(wordsToUse);
        var combined = string.Join(" ", shuffled);
        return combined;
    }

    /// <summary>
    ///     This event is triggered when the parrot hears someone speaking. If allowed, the parrot may learn it.
    ///     Now we remove punctuation from the message, split into words, pick a random sub-chunk, and save both:
    ///     - The whole sub-chunk as single or multi-word phrase
    ///     - Each word from that sub-chunk as a single word
    /// </summary>
    private void OnListen(EntityUid uid, ParrotSpeechComponent component, ref ListenEvent args)
    {
        // Random chance to learn
        if (!_random.Prob(component.LearnChance))
            return;

        // 1) Remove punctuation (replace it with spaces), convert to lower-case
        var cleaned = RemovePunctuationAndToLower(args.Message);

        // 2) Split into words
        var words = cleaned.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return;

        // 3) Decide how many words we pick from the overheard message
        var phraseLength = 1 + (int)(Math.Sqrt(_random.NextDouble()) * component.MaximumPhraseLength);
        if (phraseLength > words.Length)
            phraseLength = words.Length;

        // 4) Pick a random start index
        var startIndex = _random.Next(0, Math.Max(1, words.Length - phraseLength + 1));
        var chunk = words.Skip(startIndex).Take(phraseLength).ToArray();

        // 5) If chunk has only 1 word, store it as single word
        //    otherwise store it as a multi-word phrase
        if (chunk.Length == 1)
        {
            LearnSingleWord(chunk[0], component);
        }
        else
        {
            var phrase = string.Join(" ", chunk);
            LearnMultiWord(phrase, component);
        }

        // 6) Independently, store all words of that chunk as single words (no duplicates)
        foreach (var w in chunk)
        {
            LearnSingleWord(w, component);
        }
    }

    /// <summary>
    ///     Checks if the source is blacklisted. If so, the parrot won't listen.
    /// </summary>
    private void OnListenAttempt(EntityUid uid, ParrotSpeechComponent component, ref ListenAttemptEvent args)
    {
        if (_whitelistSystem.IsBlacklistPass(component.Blacklist, args.Source))
            args.Cancel();
    }

    /// <summary>
    ///     Adds a single word into the SingleWordPhrases set, removing a random word if we exceed the limit.
    /// </summary>
    private void LearnSingleWord(string word, ParrotSpeechComponent component)
    {
        // If we already have it, skip
        if (component.SingleWordPhrases.Contains(word))
            return;

        // If we exceed maximum, remove a random single word
        if (component.SingleWordPhrases.Count >= component.MaximumSingleWordCount)
        {
            var list = component.SingleWordPhrases.ToList();
            var toRemove = _random.Pick(list);
            component.SingleWordPhrases.Remove(toRemove);
        }

        component.SingleWordPhrases.Add(word);
    }

    /// <summary>
    ///     Adds a multi-word phrase into the MultiWordPhrases set, removing a random phrase if we exceed the limit.
    /// </summary>
    private void LearnMultiWord(string phrase, ParrotSpeechComponent component)
    {
        // If we already have it, skip
        if (component.MultiWordPhrases.Contains(phrase))
            return;

        // If we exceed maximum, remove a random multi-word phrase
        if (component.MultiWordPhrases.Count >= component.MaximumMultiWordCount)
        {
            var list = component.MultiWordPhrases.ToList();
            var toRemove = _random.Pick(list);
            component.MultiWordPhrases.Remove(toRemove);
        }

        component.MultiWordPhrases.Add(phrase);
    }

    /// <summary>
    ///     Replaces all punctuation with spaces and returns a lower-cased string.
    ///     E.g. "Hello, world! I'm here." => "hello  world  i m here "
    ///     then trimmed/split => "hello", "world", "i", "m", "here"
    /// </summary>
    private string RemovePunctuationAndToLower(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsPunctuation(chars[i]))
            {
                chars[i] = ' ';
            }
        }

        // Convert to lower case
        return new string(chars).ToLowerInvariant();
    }
}

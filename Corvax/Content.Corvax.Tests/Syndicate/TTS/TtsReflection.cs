using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Content.Shared.Corvax.TTS;
using Robust.Shared.GameObjects;

using ClientTTS = Content.Client.Corvax.TTS.TTSSystem;
using ServerTTS = Content.Server.Corvax.TTS.TTSSystem;
using ServerTTSManager = Content.Server.Corvax.TTS.TTSManager;

namespace Content.Corvax.Tests.Syndicate.TTS;

/// <summary>
/// Reflection bridge into the TTS implementation.
/// All lookups are validated on first touch and throw a descriptive
/// <see cref="InvalidOperationException"/> when the production surface
/// changes — otherwise a silent <c>NullReferenceException</c> inside a
/// single test would hide the actual contract break.
/// </summary>
internal static class TtsReflection
{
    private static FieldInfo RequireField(Type type, string name)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new InvalidOperationException(
                $"TTS reflection contract broken: {type.FullName}.{name} field not found. " +
                "Update TtsReflection to match the current implementation.");
        }

        return field;
    }

    private static MethodInfo RequireMethod(Type type, string name)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
            throw new InvalidOperationException(
                $"TTS reflection contract broken: {type.FullName}.{name}() not found. " +
                "Update TtsReflection to match the current implementation.");
        return method;
    }

    private static Type RequireNestedType(Type type, string name)
    {
        var nested = type.GetNestedType(name, BindingFlags.NonPublic);
        if (nested is null)
            throw new InvalidOperationException(
                $"TTS reflection contract broken: nested type {type.FullName}+{name} not found. " +
                "Update TtsReflection to match the current implementation.");
        return nested;
    }

    private static readonly FieldInfo ServerCacheField =
        RequireField(typeof(ServerTTSManager), "_cache");

    private static readonly FieldInfo ServerCacheKeysField =
        RequireField(typeof(ServerTTSManager), "_cacheKeysSeq");

    private static readonly MethodInfo ServerSanitizeMethod =
        RequireMethod(typeof(ServerTTS), "Sanitize");

    private static readonly MethodInfo ServerToSsmlMethod =
        RequireMethod(typeof(ServerTTS), "ToSsmlText");

    private static readonly Type ServerSoundTraitsType =
        RequireNestedType(typeof(ServerTTS), "SoundTraits");

    public static readonly FieldInfo ClientPlayingField =
        RequireField(typeof(ClientTTS), "_playingEntities");

    public static readonly FieldInfo ClientQueuesField =
        RequireField(typeof(ClientTTS), "_entityQueues");

    public static readonly FieldInfo ClientEnabledField =
        RequireField(typeof(ClientTTS), "_ttsEnabled");

    public static readonly FieldInfo ClientRadioVolumeField =
        RequireField(typeof(ClientTTS), "_radioVolume");

    public static readonly FieldInfo ClientVolumeField =
        RequireField(typeof(ClientTTS), "_volume");

    private static int TraitValue(string name)
    {
        if (!Enum.IsDefined(ServerSoundTraitsType, name))
        {
            throw new InvalidOperationException(
                $"TTS reflection contract broken: SoundTraits.{name} is not defined. " +
                "Update TtsReflection to match the current enum.");
        }

        return Convert.ToInt32(Enum.Parse(ServerSoundTraitsType, name));
    }

    private static readonly int NormalTraits =
        TraitValue("RateFast") | TraitValue("PitchMedium");

    private static readonly int WhisperTraits =
        TraitValue("RateSlow") | TraitValue("PitchVerylow") | TraitValue("VolumeXSoft");

    public static string CallSanitize(ServerTTS system, string text)
        => (string)ServerSanitizeMethod.Invoke(system, new object[] { text })!;

    public static string CallToSsml(ServerTTS system, string sanitized, int traitsValue)
    {
        var traits = Enum.ToObject(ServerSoundTraitsType, traitsValue);
        return (string)ServerToSsmlMethod.Invoke(system, new[] { sanitized, traits })!;
    }

    /// <summary>
    /// Mirrors the SSML pipeline inside <c>ServerTTS.GenerateTTS</c>:
    /// sanitize → append trailing period if the text ends in a letter → wrap via <c>ToSsmlText</c>.
    /// The period step is duplicated because it lives inside <c>GenerateTTS</c> itself,
    /// which would otherwise trigger the HTTP-backed <see cref="ServerTTSManager"/>.
    /// If the production pipeline changes, the cache key will not match and
    /// <c>RunsWithCachedAudio</c> will fall through to the network — a real failure,
    /// not a silent pass.
    /// </summary>
    public static string BuildSsmlFor(ServerTTS system, string text, bool isWhisper)
    {
        var sanitized = CallSanitize(system, text);
        if (string.IsNullOrEmpty(sanitized))
            return string.Empty;

        if (char.IsLetter(sanitized[^1]))
            sanitized += ".";

        var traits = isWhisper ? WhisperTraits : NormalTraits;
        return CallToSsml(system, sanitized, traits);
    }

    public static void ClearServerCache(ServerTTSManager manager)
    {
        var cache = (Dictionary<string, byte[]>)ServerCacheField.GetValue(manager)!;
        var keys = (List<string>)ServerCacheKeysField.GetValue(manager)!;
        cache.Clear();
        keys.Clear();
    }

    public static void PrepopulateServerCache(
        ServerTTSManager manager, string speaker, string ssml, byte[] data)
    {
        var cache = (Dictionary<string, byte[]>)ServerCacheField.GetValue(manager)!;
        var keys = (List<string>)ServerCacheKeysField.GetValue(manager)!;

        var key = BuildCacheKey(speaker, ssml);
        cache[key] = data;
        keys.Add(key);
    }

    /// <summary>
    /// Sanity helper: verify that a seeded cache entry is actually present,
    /// so <c>RunsWithCachedAudio</c> fails at the setup step rather than
    /// silently making a real network call.
    /// </summary>
    public static bool HasServerCacheEntry(ServerTTSManager manager, string speaker, string ssml)
    {
        var cache = (Dictionary<string, byte[]>)ServerCacheField.GetValue(manager)!;
        return cache.ContainsKey(BuildCacheKey(speaker, ssml));
    }

    private static string BuildCacheKey(string speaker, string ssml)
    {
        var rawKey = $"{speaker}/{ssml.ToLowerInvariant()}";
        return rawKey.Length <= 32
            ? rawKey
            : Convert.ToHexString(
                System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(rawKey)));
    }

    public static void ClearClientState(ClientTTS tts)
    {
        ((Dictionary<NetEntity, Queue<PlayTTSEvent>>)ClientQueuesField.GetValue(tts)!).Clear();
        ((HashSet<NetEntity>)ClientPlayingField.GetValue(tts)!).Clear();
    }
}

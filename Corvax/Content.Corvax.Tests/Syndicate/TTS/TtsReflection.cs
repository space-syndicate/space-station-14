using System.Reflection;

using ServerTTS = Content.Server.Corvax.TTS.TTSSystem;

namespace Content.Corvax.Tests.Syndicate.TTS;

/// <summary>
/// Reflection bridge into the TTS implementation.
/// Lookups are validated on first touch and throw <see cref="InvalidOperationException"/>
/// if the production surface changes, so a contract break fails loudly instead of
/// surfacing as a silent <c>NullReferenceException</c> in one test.
/// </summary>
internal static class TtsReflection
{
    private static MethodInfo RequireMethod(Type type, string name)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException(
                $"TTS reflection contract broken: {type.FullName}.{name}() not found. " +
                "Update TtsReflection to match the current implementation.");
        }
        return method;
    }

    private static Type RequireNestedType(Type type, string name)
    {
        var nested = type.GetNestedType(name, BindingFlags.NonPublic);
        if (nested is null)
        {
            throw new InvalidOperationException(
                $"TTS reflection contract broken: nested type {type.FullName}+{name} not found. " +
                "Update TtsReflection to match the current implementation.");
        }
        return nested;
    }

    private static readonly MethodInfo ServerSanitizeMethod =
        RequireMethod(typeof(ServerTTS), "Sanitize");

    private static readonly MethodInfo ServerToSsmlMethod =
        RequireMethod(typeof(ServerTTS), "ToSsmlText");

    private static readonly Type ServerSoundTraitsType =
        RequireNestedType(typeof(ServerTTS), "SoundTraits");

    public static string CallSanitize(ServerTTS system, string text)
        => (string)ServerSanitizeMethod.Invoke(system, new object[] { text })!;

    public static string CallToSsml(ServerTTS system, string sanitized, int traitsValue)
    {
        var traits = Enum.ToObject(ServerSoundTraitsType, traitsValue);
        return (string)ServerToSsmlMethod.Invoke(system, new[] { sanitized, traits })!;
    }
}

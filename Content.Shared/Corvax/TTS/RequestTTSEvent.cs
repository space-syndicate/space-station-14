using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public sealed class RequestTTSEvent : EntityEventArgs
{
    public string Text { get; }

    public RequestTTSEvent(string text)
    {
        Text = text;
    }
}

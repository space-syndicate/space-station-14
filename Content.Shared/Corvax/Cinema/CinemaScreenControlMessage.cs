using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Cinema;

/// <summary>Playback request validated by the screen's server-side bound UI handler.</summary>
[Serializable, NetSerializable]
public sealed class CinemaScreenControlMessage : BoundUserInterfaceMessage
{
    public CinemaScreenAction Action;
    public string Film = string.Empty;
    public string Url = string.Empty;
    public double SeekSeconds;
    public float Volume;
}

[Serializable, NetSerializable]
public sealed class CinemaScreenState(string? url, float volume, string status, string[] films, string? preparationStage = null, int preparationPercent = -1) : BoundUserInterfaceState
{
    public string? Url = url;
    public float Volume = volume;
    public string Status = status;
    public string[] Films = films;
    public string? PreparationStage = preparationStage;
    public int PreparationPercent = preparationPercent;
}

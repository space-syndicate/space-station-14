namespace Content.Shared.Corvax.Cinema;

/// <summary>
/// Actions a client can request for a cinema screen.
/// The server always validates and applies them (never trusted from the client).
/// </summary>
public enum CinemaScreenAction : byte
{
    SetUrl,
    Play,
    Pause,
    Stop,
    Seek,
    SetVolume,
    SelectFilm,
}

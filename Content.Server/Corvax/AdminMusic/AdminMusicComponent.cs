namespace Content.Server.Corvax.AdminMusic;

/// <summary>
///     Метка аудио-сущности, созданной <see cref="AdminMusicSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(AdminMusicSystem))]
public sealed partial class AdminMusicComponent : Component
{
    [ViewVariables]
    public Guid TrackId;
}

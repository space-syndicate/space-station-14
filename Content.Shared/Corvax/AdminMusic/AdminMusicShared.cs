using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Corvax.AdminMusic;

public static class AdminMusicConstants
{
    /// <summary>
    ///     Ключ передачи файла от администратора на сервер.
    /// </summary>
    public const string UploadTransferKey = "CorvaxAdminMusicUpload";

    /// <summary>
    ///     Ключ передачи файла с сервера клиенту.
    /// </summary>
    public const string DownloadTransferKey = "CorvaxAdminMusicDownload";

    public const int MaxTitleLength = 128;

    public static readonly ResPath Prefix = new("/CorvaxAdminMusic");

    /// <summary>
    ///     Путь файла внутри content root, примонтированного по <see cref="Prefix"/>.
    /// </summary>
    public static ResPath GetTrackFileName(Guid id)
    {
        return new ResPath($"{id}.ogg");
    }

    /// <summary>
    ///     Полный путь, по которому трек ищут аудио-система и кэш ресурсов.
    /// </summary>
    public static ResPath GetTrackPath(Guid id)
    {
        return Prefix / GetTrackFileName(id);
    }

    public static bool IsOgg(ReadOnlySpan<byte> data)
    {
        return data.Length >= 4
               && data[0] == (byte)'O'
               && data[1] == (byte)'g'
               && data[2] == (byte)'g'
               && data[3] == (byte)'S';
    }
}

/// <summary>
///     Отправляется клиентом после получения (или ошибки получения) трека.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminMusicDownloadedEvent : EntityEventArgs
{
    public Guid TrackId;
    public bool Success;

    public AdminMusicDownloadedEvent(Guid trackId, bool success)
    {
        TrackId = trackId;
        Success = success;
    }
}

/// <summary>
///     Отправляется сервером, когда трек больше не нужен и клиент должен освободить память.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminMusicTrackDisposedEvent : EntityEventArgs
{
    public Guid TrackId;

    public AdminMusicTrackDisposedEvent(Guid trackId)
    {
        TrackId = trackId;
    }
}

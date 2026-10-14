using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.AdminMusic;

[Serializable, NetSerializable]
public enum AdminMusicObjectType : byte
{
    Grid,
    Map,
    Station,
}

[Serializable, NetSerializable]
public enum AdminMusicTrackStatus : byte
{
    /// <summary>
    ///     Ожидаем загрузку файла от администратора.
    /// </summary>
    Uploading,

    /// <summary>
    ///     Файл рассылается получателям.
    /// </summary>
    Distributing,

    Playing,
    Paused,
}

[Serializable, NetSerializable]
public sealed class AdminMusicPlayerData
{
    public NetUserId UserId;
    public string Username = string.Empty;
    public string? CharacterName;
    public NetEntity? Grid;
    public NetEntity? Map;
    public NetEntity? Station;
}

[Serializable, NetSerializable]
public sealed class AdminMusicObjectData
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public AdminMusicObjectType Type;
}

[Serializable, NetSerializable]
public sealed class AdminMusicTrackData
{
    public Guid Id;
    public string Title = string.Empty;
    public string AdminName = string.Empty;
    public AdminMusicTrackStatus Status;

    /// <summary>
    ///     Полная длина файла в секундах.
    /// </summary>
    public float Length;

    public float Start;
    public float End;
    public float Volume;

    /// <summary>
    ///     Серверное время, от которого считается позиция. См. <c>AudioComponent.AudioStart</c>.
    /// </summary>
    public TimeSpan AudioStart;

    /// <summary>
    ///     Серверное время постановки на паузу. См. <c>AudioComponent.PauseTime</c>.
    /// </summary>
    public TimeSpan? PauseTime;

    public int Listeners;
    public int Pending;
    public int Failed;
}

[Serializable, NetSerializable]
public sealed class AdminMusicEuiState : EuiStateBase
{
    public List<AdminMusicPlayerData> Players = new();
    public List<AdminMusicObjectData> Objects = new();
    public List<AdminMusicTrackData> Tracks = new();
}

public static class AdminMusicEuiMsg
{
    /// <summary>
    ///     Запрос на воспроизведение. Сервер отвечает <see cref="UploadPermitted"/> или <see cref="Error"/>.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class RequestPlay : EuiMessageBase
    {
        public Guid TrackId;
        public string Title = string.Empty;
        public int Size;
        public float Volume;
        public float Start;
        public float End;
        public bool StartPaused;
        public List<NetUserId> Recipients = new();
    }

    [Serializable, NetSerializable]
    public sealed class UploadPermitted : EuiMessageBase
    {
        public Guid TrackId;
    }

    [Serializable, NetSerializable]
    public sealed class Error : EuiMessageBase
    {
        public string Message = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class SetPaused : EuiMessageBase
    {
        public Guid TrackId;
        public bool Paused;
    }

    [Serializable, NetSerializable]
    public sealed class Seek : EuiMessageBase
    {
        public Guid TrackId;
        public float Position;
    }

    [Serializable, NetSerializable]
    public sealed class SetVolume : EuiMessageBase
    {
        public Guid TrackId;
        public float Volume;
    }

    [Serializable, NetSerializable]
    public sealed class Stop : EuiMessageBase
    {
        public Guid TrackId;
    }

    [Serializable, NetSerializable]
    public sealed class Refresh : EuiMessageBase
    {
    }
}

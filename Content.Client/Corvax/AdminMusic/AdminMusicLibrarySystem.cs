using System.IO;
using System.Linq;
using Content.Shared.Corvax.AdminMusic;
using Content.Shared.Corvax.CCCVars;
using Robust.Client.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Client.Corvax.AdminMusic;

/// <summary>
///     Локальная библиотека .ogg файлов администратора.
///     Файлы хранятся в пользовательской папке игры (UserData) и удаляются только вручную.
/// </summary>
public sealed partial class AdminMusicLibrarySystem : EntitySystem
{
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    public static readonly ResPath LibraryDirectory = new("/AdminMusic");
    private static readonly ResPath IndexPath = LibraryDirectory / "library.yml";

    private readonly List<AdminMusicLibraryEntry> _entries = new();
    private bool _loaded;

    public IReadOnlyList<AdminMusicLibraryEntry> Entries
    {
        get
        {
            EnsureLoaded();
            return _entries;
        }
    }

    public event Action? LibraryChanged;

    public enum AddResult : byte
    {
        Success,
        TooLarge,
        Invalid,
    }

    /// <summary>
    ///     Копирует .ogg файл в библиотеку.
    /// </summary>
    public AddResult AddTrack(Stream source, string? originalPath, out AdminMusicLibraryEntry? entry)
    {
        EnsureLoaded();
        entry = null;

        var maxSize = (long) (_cfg.GetCVar(CCCVars.AdminMusicMaxSizeMb) * 1024 * 1024);
        if (source.CanSeek && source.Length > maxSize)
            return AddResult.TooLarge;

        byte[] data;
        using (var memory = new MemoryStream())
        {
            source.CopyTo(memory);
            data = memory.ToArray();
        }

        if (data.Length > maxSize)
            return AddResult.TooLarge;

        if (!AdminMusicConstants.IsOgg(data))
            return AddResult.Invalid;

        float length;
        string? tagTitle;
        string? tagArtist;
        try
        {
            // Декодируем только чтобы проверить файл и узнать длительность с тегами.
            using var audioStream = _audioManager.LoadAudioOggVorbis(new MemoryStream(data, false));
            length = (float) audioStream.Length.TotalSeconds;
            tagTitle = audioStream.Title;
            tagArtist = audioStream.Artist;
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to load admin music file {originalPath}: {e.Message}");
            return AddResult.Invalid;
        }

        var id = Guid.NewGuid();
        var fileName = $"{id}.ogg";

        // Название берём из имени файла: теги часто содержат служебные имена вроде "Sound_10684_Alt".
        var title = string.IsNullOrWhiteSpace(originalPath)
            ? null
            : GetFileNameWithoutExtension(originalPath).Trim();

        if (string.IsNullOrEmpty(title))
            title = string.IsNullOrWhiteSpace(tagTitle) ? fileName : tagTitle;

        entry = new AdminMusicLibraryEntry
        {
            Id = id.ToString(),
            Title = title,
            TagTitle = string.IsNullOrWhiteSpace(tagTitle) ? null : tagTitle,
            Artist = string.IsNullOrWhiteSpace(tagArtist) ? null : tagArtist,
            OriginalPath = originalPath ?? string.Empty,
            FileName = fileName,
            Size = data.Length,
            Length = length,
            Added = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };

        _res.UserData.CreateDir(LibraryDirectory);
        using (var file = _res.UserData.OpenWrite(LibraryDirectory / fileName))
        {
            file.Write(data);
        }

        _entries.Add(entry);
        SaveIndex();
        LibraryChanged?.Invoke();
        return AddResult.Success;
    }

    /// <summary>
    ///     Удаляет трек из библиотеки вместе с файлом.
    /// </summary>
    public void RemoveTrack(AdminMusicLibraryEntry entry)
    {
        EnsureLoaded();

        if (!_entries.Remove(entry))
            return;

        var path = LibraryDirectory / entry.FileName;
        try
        {
            if (_res.UserData.Exists(path))
                _res.UserData.Delete(path);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to delete admin music file {path}: {e.Message}");
        }

        SaveIndex();
        LibraryChanged?.Invoke();
    }

    public byte[]? GetTrackData(AdminMusicLibraryEntry entry)
    {
        var path = LibraryDirectory / entry.FileName;

        try
        {
            return _res.UserData.Exists(path) ? _res.UserData.ReadAllBytes(path) : null;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read admin music file {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Аналог Path.GetFileNameWithoutExtension, который недоступен в песочнице клиента.
    /// </summary>
    private static string GetFileNameWithoutExtension(string path)
    {
        var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        _entries.Clear();

        if (!_res.UserData.Exists(IndexPath))
            return;

        try
        {
            using var reader = _res.UserData.OpenText(IndexPath);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0)
                return;

            var index = _serialization.Read<AdminMusicLibraryIndex>(yaml.Documents[0].RootNode.ToDataNode(),
                notNullableOverride: true);

            // Пропускаем записи, файлы которых удалили вручную.
            // Имя файла проверяем, чтобы правка library.yml не позволила выйти за пределы папки.
            _entries.AddRange(index.Tracks.Where(e =>
                Guid.TryParse(e.Id, out _)
                && e.FileName == $"{e.Id}.ogg"
                && _res.UserData.Exists(LibraryDirectory / e.FileName)));
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load admin music library: {e}");
        }
    }

    private void SaveIndex()
    {
        try
        {
            _res.UserData.CreateDir(LibraryDirectory);

            var index = new AdminMusicLibraryIndex { Tracks = _entries.ToList() };
            var node = _serialization.WriteValue(index, alwaysWrite: true, notNullableOverride: true);

            using var writer = _res.UserData.OpenWriteText(IndexPath);
            node.Write(writer);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save admin music library: {e}");
        }
    }
}

[DataDefinition]
public sealed partial class AdminMusicLibraryIndex
{
    [DataField]
    public List<AdminMusicLibraryEntry> Tracks = new();
}

[DataDefinition]
public sealed partial class AdminMusicLibraryEntry
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public string Title = string.Empty;

    /// <summary>
    ///     Название из тега TITLE внутри .ogg.
    /// </summary>
    [DataField]
    public string? TagTitle;

    [DataField]
    public string? Artist;

    /// <summary>
    ///     Путь к исходному файлу, из которого трек был добавлен.
    /// </summary>
    [DataField]
    public string OriginalPath = string.Empty;

    /// <summary>
    ///     Имя файла внутри <see cref="AdminMusicLibrarySystem.LibraryDirectory"/>.
    /// </summary>
    [DataField(required: true)]
    public string FileName = string.Empty;

    [DataField]
    public int Size;

    /// <summary>
    ///     Длительность в секундах.
    /// </summary>
    [DataField]
    public float Length;

    [DataField]
    public string Added = string.Empty;
}

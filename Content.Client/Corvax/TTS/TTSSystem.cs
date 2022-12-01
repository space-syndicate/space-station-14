using Content.Shared.Corvax.TTS;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    
    /// <summary>
    ///     The prefix for any and all downloaded temporary TTS audio.
    /// </summary>
    private static readonly ResourcePath Prefix = ResourcePath.Root / "TTS";
    private readonly MemoryContentRoot _contentRoot = new();

    public override void Initialize()
    {
        base.Initialize();
        _resourceManager.AddRoot(Prefix, _contentRoot);
        SubscribeNetworkEvent<PlayTTSMessage>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _contentRoot.Dispose();
    }

    private void OnPlayTTS(PlayTTSMessage ev)
    {
        // Not the best implementation because of resaving data into MemoryContentRoot, but without code duplication ¯\_(ツ)_/¯
        var absPath = Prefix / "temp.ogg";
        var relPath = absPath.RelativeTo(Prefix);
        _contentRoot.AddOrUpdateFile(relPath, ev.Data);
        _audio.PlayGlobal(new SoundPathSpecifier(absPath), Filter.Local(), false);
        _contentRoot.RemoveFile(relPath);
    }
}

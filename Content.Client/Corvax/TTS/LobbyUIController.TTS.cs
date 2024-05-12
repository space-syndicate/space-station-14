using Content.Client.Corvax.TTS;
using Robust.Client.UserInterface;

namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    [UISystemDependency] private readonly TTSSystem _tts = default!;

    public void PlayTTS()
    {
        // Test moment
        if (_profile == null || _stateManager.CurrentState is not LobbyState)
            return;

        _tts.RequestPreviewTTS(_profile.Voice);
    }
}

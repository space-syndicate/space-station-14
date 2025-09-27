using Content.Client.Eui;
using Content.Shared.Advertise.Systems;
using Content.Shared.Corvax.Audio;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.Corvax.Audio.UI;

[UsedImplicitly]
public sealed class AudioControlsEui : BaseEui
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private readonly AudioControls _window;

    public AudioControlsEui()
    {
        _window = new AudioControls();
        _window.OnClose += OnClose;

        _window.OnPitchChanged += pitch => SendMessage(new AudioControlsPitchChangedMessage(pitch));
        _window.OnVolumeChanged += volume => SendMessage(new AudioControlsVolumeChangedMessage(volume));
        _window.OnRangeChanged += range => SendMessage(new AudioControlsRangeChangedMessage(range));
    }

    private void OnClose()
    {
        SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AudioControlsEuiState audioState)
        {
            return;
        }

        _window.SetEntity(_entityManager.GetEntity(audioState.TargetNetEntity));
        _window.Update(audioState);
    }
}

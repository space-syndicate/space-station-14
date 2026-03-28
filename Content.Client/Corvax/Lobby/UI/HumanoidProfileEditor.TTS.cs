using Content.Client.Corvax.TTS;
using Content.Shared.Corvax.CCCVars;
using Robust.Client.UserInterface;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private TTSTab? _ttsTab;

    private void RefreshVoiceTab()
    {
        if (!_cfgManager.GetCVar(CCCVars.TTSEnabled))
            return;

        _ttsTab = new TTSTab();
        var children = new List<Control>();
        foreach (var child in TabContainer.Children)
            children.Add(child);

        TabContainer.RemoveAllChildren();

        for (int i = 0; i < children.Count; i++)
        {
            if (i == 1) // Set the tab to the 2nd place.
            {
                TabContainer.AddChild(_ttsTab);
            }
            TabContainer.AddChild(children[i]);
        }

        TabContainer.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-voice-tab"));

        _ttsTab.OnVoiceSelected += voiceId =>
        {
            SetVoice(voiceId);
            _ttsTab.SetSelectedVoice(voiceId);
        };

        _ttsTab.OnPreviewRequested += voiceId =>
        {
            _entManager.System<TTSSystem>().RequestPreviewTTS(voiceId);
        };
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null || _ttsTab is null)
            return;

        _ttsTab.UpdateControls(Profile, Profile.Sex);
        _ttsTab.SetSelectedVoice(Profile.Voice);
    }
    private void SetVoice(string newVoice)
    {
        Profile = Profile?.WithVoice(newVoice);
        IsDirty = true;
    }
}

using System.Linq;
using Content.Client.Corvax.Sponsors;
using Content.Shared.Corvax.TTS;
using Content.Shared.Humanoid;
using Robust.Shared.Network;

namespace Content.Client.Preferences.UI;

public sealed partial class HumanoidProfileEditor
{
    private IClientNetManager _net = default!;
    private List<TTSVoicePrototype> _voiceList = default!;
    private const string SampleText = "съешь ещё этих мягких французских булок, да выпей чаю";

    private void InitializeVoice()
    {
        _net = IoCManager.Resolve<IClientNetManager>();
        _voiceList = _prototypeManager.EnumeratePrototypes<TTSVoicePrototype>().Where(o => o.RoundStart).ToList();

        _voiceButton.OnItemSelected += args =>
        {
            _voiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };
            
        _voicePlayButton.OnPressed += _ => { PlayTTS(); };
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null)
            return;

        _voiceButton.Clear();

        var firstVoiceChoiceId = 1;
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (voice.Sex != Sex.Unsexed && voice.Sex != Profile.Sex)
                continue;
                
            var name = Loc.GetString(voice.Name);
            _voiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == 1)
                firstVoiceChoiceId = i;

            if (voice.SponsorOnly &&
                IoCManager.Resolve<SponsorsManager>().TryGetInfo(out var sponsor) &&
                !sponsor.AllowedMarkings.Contains(voice.ID))
            {
                _voiceButton.SetItemDisabled(i, true);
            }
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.Voice);
        if (!_voiceButton.TrySelectId(voiceChoiceId) &&
            _voiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetVoice(_voiceList[firstVoiceChoiceId].ID);
        }
    }

    private void PlayTTS()
    {
        if (_previewDummy is null || Profile is null)
            return;
        
        var msg = new RequestTTSEvent() { Text = SampleText, Uid = _previewDummy.Value, VoiceId = Profile.Voice };
        _net.ClientSendMessage(msg);
    }
}

using System.Linq;
using Content.Client.Corvax.Sponsors;
using Content.Shared.Corvax.TTS;
using Content.Shared.Humanoid;

namespace Content.Client.Preferences.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<TTSVoicePrototype> _voiceList = default!; // Corvax-TTS

    private void InitializeVoice()
    {
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
        
        var sponsorsManager = IoCManager.Resolve<SponsorsManager>();

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
                sponsorsManager.TryGetInfo(out var sponsor) &&
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
        
    }
}

using System.Linq;
using Content.Client.Corvax.TTS;
using Content.Client.Lobby;
using Content.Shared.Corvax.TTS;
using Content.Shared.Preferences;
using Content.Corvax.Interfaces.Client;

namespace Content.Client.Preferences.UI;

public sealed partial class HumanoidProfileEditor
{
    private IClientSponsorsManager? _sponsorsMgr;
    private List<TTSVoicePrototype> _voiceList = default!;

    private void InitializeVoice()
    {
        _voiceList = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        _voiceButton.OnItemSelected += args =>
        {
            _voiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        _voicePlayButton.OnPressed += _ => { UserInterfaceManager.GetUIController<LobbyUIController>().PlayTTS(); };
        IoCManager.Instance!.TryResolveType(out _sponsorsMgr);
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
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            _voiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == 1)
                firstVoiceChoiceId = i;

            if (_sponsorsMgr is null)
                continue;
            if (voice.SponsorOnly && _sponsorsMgr != null &&
                !_sponsorsMgr.Prototypes.Contains(voice.ID))
            {
                _voiceButton.SetItemDisabled(_voiceButton.GetIdx(i), true);
            }
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.Voice);
        if (!_voiceButton.TrySelectId(voiceChoiceId) &&
            _voiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetVoice(_voiceList[firstVoiceChoiceId].ID);
        }
    }
}

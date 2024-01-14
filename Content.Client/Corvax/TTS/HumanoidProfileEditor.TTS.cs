using System.Linq;
using Content.Client.Corvax.TTS;
using Content.Shared.Corvax.TTS;
using Content.Shared.Preferences;
using Robust.Shared.Random;
using Content.Corvax.Interfaces.Client;

namespace Content.Client.Preferences.UI;

public sealed partial class HumanoidProfileEditor
{
    private IRobustRandom _random = default!;
    private TTSSystem _ttsSys = default!;
    private IClientSponsorsManager? _sponsorsMgr;
    private List<TTSVoicePrototype> _voiceList = default!;
    private readonly List<string> _sampleText = new()
    {
        "Съешь же ещё этих мягких французских булок, да выпей чаю.",
        "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
        "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
        "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!",
        "Учёные, тут странная аномалия в баре! Она уже съела мима!",
        "Я надеюсь что инженеры внимательно следят за сингулярностью...",
        "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
        "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
        "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
        "Вам нужно согласие и печать квартирмейстера, если вы хотите сделать заказ на партию дробовиков.",
        "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
        "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
    };

    private void InitializeVoice()
    {
        _random = IoCManager.Resolve<IRobustRandom>();
        _ttsSys = _entMan.System<TTSSystem>();
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

        _voicePlayButton.OnPressed += _ => { PlayTTS(); };
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

    private void PlayTTS()
    {
        if (_previewDummy is null || Profile is null)
            return;

        _ttsSys.RequestGlobalTTS(_random.Pick(_sampleText), Profile.Voice);
    }
}

using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed class LizardAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");

    // Corvax-Localization-Start
    private static readonly Regex _regexLowerC = new Regex("с+");
    private static readonly Regex _regexUpperC = new Regex("С+");
    private static readonly Regex _regexLowerZ = new Regex("з+");
    private static readonly Regex _regexUpperZ = new Regex("З+");
    private static readonly Regex _regexLowerSh = new Regex("ш+");
    private static readonly Regex _regexUpperSh = new Regex("Ш+");
    private static readonly Regex _regexLowerCh = new Regex("ч+");
    private static readonly Regex _regexUpperCh = new Regex("Ч+");
    private static readonly List<string> _replacementsSs = new List<string> { "сс", "ссс" };
    private static readonly List<string> _replacementsSsUpper = new List<string> { "СС", "ССС" };
    private static readonly List<string> _replacementsSh = new List<string> { "шш", "шшш" };
    private static readonly List<string> _replacementsShUpper = new List<string> { "ШШ", "ШШШ" };
    private static readonly List<string> _replacementsCh = new List<string> { "щщ", "щщщ" };
    private static readonly List<string> _replacementsChUpper = new List<string> { "ЩЩ", "ЩЩЩ" };
    // Corvax-Localization-End

    [Dependency] private readonly IRobustRandom _random = default!; // Corvax-Localization

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");

        // Corvax-Localization-Start
        message = _regexLowerC.Replace(message, _random.Pick(_replacementsSs));
        message = _regexUpperC.Replace(message, _random.Pick(_replacementsSsUpper));
        message = _regexLowerZ.Replace(message, _random.Pick(_replacementsSs));       // для "з+" используются те же замены, что и для "с+"
        message = _regexUpperZ.Replace(message, _random.Pick(_replacementsSsUpper)); // для "З+" используются те же замены, что и для "С+"
        message = _regexLowerSh.Replace(message, _random.Pick(_replacementsSh));
        message = _regexUpperSh.Replace(message, _random.Pick(_replacementsShUpper));
        message = _regexLowerCh.Replace(message, _random.Pick(_replacementsCh));
        message = _regexUpperCh.Replace(message, _random.Pick(_replacementsChUpper));
        // Corvax-Localization-End
        args.Message = message;
    }
}

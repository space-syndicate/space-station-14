using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random; // Corvax-Localization

namespace Content.Server.Speech.EntitySystems;

public sealed class FrontalLispSystem : EntitySystem
{
    // @formatter:off
    private static readonly Regex RegexUpperTh = new(@"[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new(@"[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new(@"[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new(@"[e]+[x]+[c]*|[x]+");
    // @formatter:on

    // Corvax-Localization Start
    private static readonly Regex _regexLowerC = new Regex("с");// для "с" на "ш"/"с"
    private static readonly Regex _regexUpperC = new Regex("С");// для "С" на "Ш"/"С"
    private static readonly Regex _regexLowerCh = new Regex("ч");// для "ч" на "ш"/"ч"
    private static readonly Regex _regexUpperCh = new Regex("Ч");// для "Ч" на "Ш"/"Ч"
    private static readonly Regex _regexLowerTs = new Regex("ц");// для "ц" на "ч"/"ц"
    private static readonly Regex _regexUpperTs = new Regex("Ц");// для "Ц" на "Ч"/"Ц"
    private static readonly Regex _regexLowerT = new Regex(@"\B[т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex _regexUpperT = new Regex(@"\B[Т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex _regexLowerZ = new Regex("з");// для "з" на "ж"/"з"
    private static readonly Regex _regexUpperZ = new Regex("З");// для "З" на "Ж"/"З"
    // Corvax-Localization End

    [Dependency] private readonly IRobustRandom _random = default!; // Corvax-Localization

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrontalLispComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, FrontalLispComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // handles ts, sc(i|e|y), c(i|e|y), ps, st(io(u|n)), ch(i|e), z, s
        message = RegexUpperTh.Replace(message, "TH");
        message = RegexLowerTh.Replace(message, "th");
        // handles ex(c), x
        message = RegexUpperEcks.Replace(message, "EKTH");
        message = RegexLowerEcks.Replace(message, "ekth");

        // Corvax-Localization Start
        message = _regexLowerC.Replace(message, _random.Prob(0.90f) ? "ш" : "с");
        message = _regexUpperC.Replace(message, _random.Prob(0.90f) ? "Ш" : "С");
        message = _regexLowerCh.Replace(message, _random.Prob(0.90f) ? "ш" : "ч");
        message = _regexUpperCh.Replace(message, _random.Prob(0.90f) ? "Ш" : "Ч");
        message = _regexLowerTs.Replace(message, _random.Prob(0.90f) ? "ч" : "ц");
        message = _regexUpperTs.Replace(message, _random.Prob(0.90f) ? "Ч" : "Ц");
        message = _regexLowerT.Replace(message, _random.Prob(0.90f) ? "ч" : "т");
        message = _regexUpperT.Replace(message, _random.Prob(0.90f) ? "Ч" : "Т");
        message = _regexLowerZ.Replace(message, _random.Prob(0.90f) ? "ж" : "з");
        message = _regexUpperZ.Replace(message, _random.Prob(0.90f) ? "Ж" : "З");
        // Corvax-Localization End

        args.Message = message;
    }
}

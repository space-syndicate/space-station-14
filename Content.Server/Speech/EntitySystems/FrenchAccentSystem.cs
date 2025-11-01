using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

/// <summary>
/// System that gives the speaker a faux-French accent.
/// </summary>
public sealed class FrenchAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;
    [Dependency] private readonly IRobustRandom _random = default!; // Russian-Locale

    private static readonly Regex RegexTh = new(@"th", RegexOptions.IgnoreCase);
    //Russian-Locale-Start
    private static readonly Regex RegexH = new("х", RegexOptions.IgnoreCase);
    private static readonly Regex RegexR = new("[рР]+", RegexOptions.IgnoreCase);
    private static readonly Regex RegexE = new("е", RegexOptions.IgnoreCase);
    private static readonly Regex RegexZ = new("з", RegexOptions.IgnoreCase);
    //Russian-Locale-End
    private static readonly Regex RegexSpacePunctuation = new(@"(?<=\w\w)[!?;:](?!\w)", RegexOptions.IgnoreCase);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FrenchAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public string Accentuate(string message, FrenchAccentComponent component)
    {
        var msg = message;

        msg = _replacement.ApplyReplacements(msg, "french");

        // spaces out ! ? : and ;.
        msg = RegexSpacePunctuation.Replace(msg, " $&");

        // replaces th with 'z or 's depending on the case
        foreach (Match match in RegexTh.Matches(msg))
        {
            var uppercase = msg.Substring(match.Index, 2).Contains("TH");
            var Z = uppercase ? "Z" : "z";
            var S = uppercase ? "S" : "s";
            var idxLetter = match.Index + 2;

            // If th is alone, just do 'z
            if (msg.Length <= idxLetter) {
                msg = msg.Substring(0, match.Index) + "'" + Z;
            } else {
                var c = "aeiouy".Contains(msg.Substring(idxLetter, 1).ToLower()) ? Z : S;
                msg = msg.Substring(0, match.Index) + "'" + c + msg.Substring(idxLetter);
            }
        }

        // Russian-Locale-Start

        foreach (Match match in RegexH.Matches(msg))
        {
            var uppercase = msg[match.Index] == 'Х';
            var g = uppercase ? "Г" : "г";

            // Если х стоит первой, то с некоторым шансом превращаем её в апостроф
            if (match.Index == 0 && _random.Prob(0.5f) || match.Index != 0 && char.IsWhiteSpace(msg[match.Index - 1]))
            {
                msg = "'" + msg[(match.Index + 1)..];
            }
            // В противном случае, тупо конвентируем её в г
            else
            {
                msg = msg[..match.Index] + g + msg[(match.Index + 1)..];
            }
        }

        // Тут через это, потому-что при работе со списком он мутирует.
        msg = RegexR.Replace(msg, match =>
        {
            var uppercase = msg[match.Index] == 'Р';
            var g = uppercase ? "Г" : "г";
            var h = "";
            for (var i = 0; i < match.Value.Length; i++)
            {
                // Делает х заглавным, только если Р большая, но не первая буква в слове, и сообщении в целом. Также капитализирует её, след. после неё буква заглавная.
                if (uppercase && match.Index != 0 && !char.IsWhiteSpace(msg[match.Index - 1]) || msg[match.Index + 1].ToString().Equals(msg[match.Index + 1].ToString().ToUpper(), StringComparison.CurrentCulture))
                    h += "Х";
                else
                    h += "х";
            }

            // Превращаем р в гх
            return g + h;
        });

        foreach (Match match in RegexE.Matches(msg))
        {
            var uppercase = msg[match.Index] == 'Е';
            var e = uppercase ? "Э" : "э";

            // Превращаем е в э перед другими буквами
            if (match.Index != 0 && !char.IsWhiteSpace(msg[match.Index - 1]))
            {
                msg = msg[..match.Index] + e + msg[(match.Index + 1)..];
            }
        }

        foreach (Match match in RegexZ.Matches(msg))
        {
            var uppercase = msg[match.Index] == 'З';
            var z = uppercase ? "Ж" : "ж";

            // С шансом в 10 процентов превращаем з в ж для изображения звонкости
            // ТУДУ: хз, мб ещё тильду после з влепливать с некоторым шансом?
            if (_random.Prob(0.1f))
            {
                msg = msg[..match.Index] + z + msg[(match.Index + 1)..];
            }
        }

        // Russian-Locale-End

        return msg;

    }

    private void OnAccentGet(EntityUid uid, FrenchAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, component);
    }
}

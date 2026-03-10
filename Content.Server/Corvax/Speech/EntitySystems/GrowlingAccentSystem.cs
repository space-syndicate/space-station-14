using System.Text.RegularExpressions;
using Content.Server.Corvax.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Corvax.Speech.EntitySystems;

public sealed class GrowlingAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly Regex _regexLowerR = new Regex("r+");
    private static readonly Regex _regexUpperR = new Regex("R+");
    private static readonly Regex _regexLowerRp = new Regex("р+");
    private static readonly Regex _regexUpperRp = new Regex("Р+");
    private static readonly List<string> _replacementsR = new List<string> { "rr", "rrr" };
    private static readonly List<string> _replacementsRUpper = new List<string> { "RR", "RRR" };
    private static readonly List<string> _replacementsRp = new List<string> { "рр", "ррр" };
    private static readonly List<string> _replacementsRpUpper = new List<string> { "РР", "РРР" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrowlingAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, GrowlingAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        message = _regexLowerR.Replace(message, _random.Pick(_replacementsR));
        message = _regexUpperR.Replace(message, _random.Pick(_replacementsRUpper));
        message = _regexLowerRp.Replace(message, _random.Pick(_replacementsRp));
        message = _regexUpperRp.Replace(message, _random.Pick(_replacementsRpUpper));

        args.Message = message;
    }
}

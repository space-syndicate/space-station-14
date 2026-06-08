using System.Text.RegularExpressions;
using Content.Server.Corvax.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Corvax.Speech.EntitySystems;

public sealed partial class GrowlingAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private static readonly Regex _regexLowerR = new Regex("r+", RegexOptions.Compiled);
    private static readonly Regex _regexUpperR = new Regex("R+", RegexOptions.Compiled);
    private static readonly Regex _regexLowerRp = new Regex("р+", RegexOptions.Compiled);
    private static readonly Regex _regexUpperRp = new Regex("Р+", RegexOptions.Compiled);
    private static readonly string[] _replacementsR = { "rr", "rrr" };
    private static readonly string[] _replacementsRUpper = { "RR", "RRR" };
    private static readonly string[] _replacementsRp = { "рр", "ррр" };
    private static readonly string[] _replacementsRpUpper = { "РР", "РРР" };

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

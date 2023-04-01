using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
namespace Content.Server.Speech.EntitySystems;

/// <summary>
/// This handles the logic and function by which the possesor of the
/// <see cref="TrulyGoodAndHonestPureOfHeartChristianBoyAccentComponent"/>
/// can use to absolve themselves of the ability to commit sin and hate unto
/// god's pure and lovely world.
/// </summary>
public sealed class TrulyGoodAndHonestPureOfHeartChristianBoyAccentSystem : EntitySystem
{
    private static readonly Dictionary<string, string> DirectReplacements = new()
    {
        // Corvax-Localization-Start
        { "блять", "непотребная женщина" },
        { "блядь", "непотребная женщина" },
        { "бля", "уф" },
        { "сука", "негодяй" },
        { "пиздец", "катастрофа" },
        { "ебать", "осуществлять половой акт" },
        { "блядский", "чрезвычайно" },
        { "ёбанный", "исключительно" },
        { "ебанный", "крайне" },
        { "ебучий", "весьма" },
        { "хуево", "плохо" },
        { "хуёво", "отвратительно" },
        { "хуй", "мужской половой орган" },
        { "мудак", "неприятный человек" },
        { "еблан", "неприятный человек" },
        { "хуйло", "неприятный человек" },
        { "долбаеб", "неприятный человек" },
        { "пиздато", "отлично" },
        { "заебись", "очень хорошо" },
        { "охуено", "замечательно" },
        { "охуительно", "невероятно" },
        { "заебал", "заколебал" },
        { "доебал", "задолбал" },
        { "отъебись", "отстань" },
        { "убить", "полюбить" },
        { "убей", "полюби" },
        { "бог", "Бог" },
        { "аду", "злом доме врага бога" },
        // Corvax-Localization-End
        { "fuck", "frick" },
        { "shit", "poop" },
        { "ass", "butt" },
        { "dick", "peter-pecker" },
        { "bitch", "nice woman" },
        { "piss", "pee" },
        { "damn", "beaver dam" },
        { "kill", "love" },
        { "hurt", "love" },
        { "god", "God" },
        { "hell", "the evil home of the enemy of god" }
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrulyGoodAndHonestPureOfHeartChristianBoyAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public string Accentuate(string message)
    {
        var msg = message;

        foreach (var (first, replace) in DirectReplacements)
        {
            msg = Regex.Replace(msg, $@"{first}", replace, RegexOptions.IgnoreCase);
        }

        return msg;
    }

    private void OnAccentGet(EntityUid uid, TrulyGoodAndHonestPureOfHeartChristianBoyAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}

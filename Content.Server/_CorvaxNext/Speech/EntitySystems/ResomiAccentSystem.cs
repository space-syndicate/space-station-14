using System.Text.RegularExpressions;
using Content.Server._CorvaxNext.Speech.Components;
using Content.Server.Speech;
using Robust.Shared.Random;

namespace Content.Server._CorvaxNext.Speech.EntitySystems;

public sealed class ResomiAccentSystem : EntitySystem
{

    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResomiAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ResomiAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // ш => шшш
        message = Regex.Replace(
            message,
            "ш+",
            _random.Pick(new List<string>() { "шш", "шшш" })
        );
        // Ш => ШШШ
        message = Regex.Replace(
            message,
            "Ш+",
            _random.Pick(new List<string>() { "ШШ", "ШШШ" })
        );
        // ч => щщщ
        message = Regex.Replace(
            message,
            "ч+",
            _random.Pick(new List<string>() { "щщ", "щщщ" })
        );
        // Ч => ЩЩЩ
        message = Regex.Replace(
            message,
            "Ч+",
            _random.Pick(new List<string>() { "ЩЩ", "ЩЩЩ" })
        );
        // р => ррр
        message = Regex.Replace(
            message,
            "р+",
            _random.Pick(new List<string>() { "рр", "ррр" })
        );
        // Р => РРР
        message = Regex.Replace(
            message,
            "Р+",
            _random.Pick(new List<string>() { "РР", "РРР" })
        );
        args.Message = message;
    }
}

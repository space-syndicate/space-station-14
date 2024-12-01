using Content.Server._CorvaxNext.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server._CorvaxNext.Speech.EntitySystems;

public sealed class OhioAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OhioAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, OhioAccentComponent component, ref AccentGetEvent args)
    {
        args.Message = Accentuate("ohio", "accent-ohio-prefix-", "accent-ohio-suffix-", args);
    }

    private string Accentuate(string accentName, string prefix, string suffix, AccentGetEvent args) {
        var message = args.Message;

        message = _replacement.ApplyReplacements(message, accentName);

        // Prefix
        if (_random.Prob(0.15f))
        {
            var pick = _random.Next(1, 7);

            // Reverse sanitize capital
            message = message[0].ToString().ToLower() + message.Remove(0, 1);
            message = Loc.GetString(prefix + pick) + " " + message;
        }

        // Sanitize capital again, in case we substituted a word that should be capitalized
        message = message[0].ToString().ToUpper() + message.Remove(0, 1);

        // Suffixes
        if (_random.Prob(0.3f))
        {
            var pick = _random.Next(1, 7);
            message += Loc.GetString(suffix + pick);
        }

        return message;
    }
};

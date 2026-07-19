using System.Text;
using Content.Server.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class MonkeyAccentSystem : RelayAccentSystem<MonkeyAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<MonkeyAccentComponent>? ent = null)
    {
        var words = message.Split();
        var accentedMessage = new StringBuilder(message.Length + 2);

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            if (_random.NextDouble() >= 0.5)
            {
                if (word.Length > 1)
                {
                    foreach (var __ in word)
                    {
                        accentedMessage.Append('У');  // Corvax-Localization
                    }

                    if (_random.NextDouble() >= 0.3)
                        accentedMessage.Append('К');  // Corvax-Localization
                }
                else
                    accentedMessage.Append('У');  // Corvax-Localization
            }
            else
            {
                foreach (var __ in word)
                {
                    if (_random.NextDouble() >= 0.8)
                        accentedMessage.Append('Г');  // Corvax-Localization
                    else
                        accentedMessage.Append('А');  // Corvax-Localization
                }

            }

            if (i < words.Length - 1)
                accentedMessage.Append(' ');
        }

        accentedMessage.Append('!');

        return accentedMessage.ToString();
    }
}

using System.Linq;
using System.Text.RegularExpressions;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Content.Shared.Corvax.CCCVars;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController
{
    private readonly List<ReplacementRule> _replacements = new();

    private static readonly Regex ReplacementStartDoubleQuote = new("\"$");
    private static readonly Regex ReplacementEndDoubleQuote = new("^\"|(?<=^@)\"");
    private static readonly Regex EndDeclination = new(@"(.)$"); 

    private readonly record struct ReplacementRule(string Keyword, string Replacement);

    public void UpdateReplacements(string newReplacements, bool firstLoad = false)
    {

        _replacements.Clear();

        var lines = newReplacements.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var parts = line.Split("|", 2, StringSplitOptions.TrimEntries);
            if (!line.Contains('|'))
                continue;

            if (parts.Length < 2)
            continue;
            
            var keyword = parts[0];
            var replacement = parts[1];

            keyword = keyword.Replace(@"\", @"\\");
            keyword = Regex.Escape(keyword);
            keyword = keyword.Replace(@"\[", @"\\\[");
            keyword = EndDeclination.Replace(keyword, "(?:$1)?[а-яё]*");

            if (keyword.Any(c => c == '"'))
            {
                keyword = ReplacementStartDoubleQuote.Replace(keyword, "(?!\\w)");
                keyword = ReplacementEndDoubleQuote.Replace(keyword, "(?<!\\w)");
            }

            _replacements.Add(new ReplacementRule(keyword, replacement));
        }

        _replacements.Sort((x, y) => y.Keyword.Length.CompareTo(x.Keyword.Length));
    }
}

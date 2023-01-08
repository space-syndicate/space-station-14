using System.Text.RegularExpressions;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private static readonly Dictionary<string, string> SlangReplace = new()
    {
        { "кз", "корпоративный закон" },
        { "дэк", "детектив" },
        { "дек", "детектив" },
        { "мш", "имплант защиты разума" },
        { "лкм", "левая рука" },
        { "пкм", "правая рука" },
        { "трейтор", "предатель" },
        { "инжи", "инженеры" },
        { "инжинер", "инженер" },
        { "нюка", "ядерные оперативники" },
        { "нюкеры", "ядерные оперативники" },
        { "хз", "не знаю" },
    };

    private string ReplaceWords(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        foreach (var (word, replacement) in SlangReplace)
            message = Regex.Replace(message, $"\\b{word}\\b", replacement);

        return message;
    }
}

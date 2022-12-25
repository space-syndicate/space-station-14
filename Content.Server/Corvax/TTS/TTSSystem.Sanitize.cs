using System.Text.RegularExpressions;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    private string Sanitize(string text)
    {
        text = text.Trim();
        text = Regex.Replace(text, @"<[^>]*>", "");
        text = Regex.Replace(text, @"[a-zA-Z]", ReplaceLat2Cyr, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"[^a-zA-Z0-9а-яА-ЯёЁ,!?+./ \r\n\t:—()-]", ReplaceMatchedWord, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?<=[1-90])(\.|,)(?=[1-90])", " целых ");
        text = Regex.Replace(text, @"\d+", ReplaceWord2Num);
        return text;
    }

    private string ReplaceLat2Cyr(Match oneChar)
    {
        if (ReverseTranslit.TryGetValue(oneChar.Value.ToLower(), out var replace))
            return replace;
        return oneChar.Value;
    }

    private string ReplaceMatchedWord(Match word)
    {
        if (WordReplacement.TryGetValue(word.Value.ToLower(), out var replace))
            return replace;
        return word.Value;
    }

    private string ReplaceWord2Num(Match word)
    {
        return word.Value; // TODO: Implement
    }
    
    private static readonly IReadOnlyDictionary<string, string> WordReplacement =
        new Dictionary<string, string>()
        {
            {"нт", "Эн Тэ"},
            {"смо", "Эс Мэ О"},
            {"гп", "Гэ Пэ"},
            {"рд", "Эр Дэ"},
            {"гсб", "Гэ Эс Бэ"},
            {"срп", "Эс Эр Пэ"},
            {"цк", "Цэ Каа"},
            {"рнд", "Эр Эн Дэ"},
            {"сб", "Эс Бэ"},
            {"рцд", "Эр Цэ Дэ"},
            {"брпд", "Бэ Эр Пэ Дэ"},
            {"рпд", "Эр Пэ Дэ"},
            {"рпед", "Эр Пед"},
            {"тсф", "Тэ Эс Эф"},
            {"срт", "Эс Эр Тэ"},
            {"обр", "О Бэ Эр"},
            {"кпк", "Кэ Пэ Каа"},
            {"пда", "Пэ Дэ А"},
            {"id", "Ай Ди"},
            {"мщ", "Эм Ще"},
            {"вт", "Вэ Тэ"},
            {"ерп", "Йе Эр Пэ"},
            {"се", "Эс Йе"},
            {"апц", "А Пэ Цэ"},
            {"лкп", "Эл Ка Пэ"},
            {"см", "Эс Эм"},
            {"ека", "Йе Ка"},
            {"ка", "Кэ А"},
            {"бса", "Бэ Эс Аа"},
            {"тк", "Тэ Ка"},
            {"бфл", "Бэ Эф Эл"},
            {"бщ", "Бэ Щэ"},
            {"кк", "Кэ Ка"},
            {"ск", "Эс Ка"},
            {"зк", "Зэ Ка"},
            {"ерт", "Йе Эр Тэ"},
            {"вкд", "Вэ Ка Дэ"},
            {"нтр", "Эн Тэ Эр"},
            {"пнт", "Пэ Эн Тэ"},
            {"авд", "А Вэ Дэ"},
            {"пнв", "Пэ Эн Вэ"},
            {"ссд", "Эс Эс Дэ"},
            {"кпб", "Кэ Пэ Бэ"},
            {"сссп", "Эс Эс Эс Пэ"},
            {"крб", "Ка Эр Бэ"},
            {"бд", "Бэ Дэ"},
            {"сст", "Эс Эс Тэ"},
            {"скс", "Эс Ка Эс"},
            {"икн", "И Ка Эн"},
            {"нсс", "Эн Эс Эс"},
            {"емп", "Йе Эм Пэ"},
            {"бс", "Бэ Эс"},
            {"цкс", "Цэ Ка Эс"},
            {"срд", "Эс Эр Дэ"},
            {"жпс", "Джи Пи Эс"},
            {"gps", "Джи Пи Эс"},
            {"ннксс", "Эн Эн Ка Эс Эс"},
            {"ss", "Эс Эс"},
            {"сс", "Эс Эс"},
            {"тесла", "тэсла"},
            {"трейзен", "трэйзэн"},
            {"нанотрейзен", "нанотрэйзэн"},
            {"рпзд", "Эр Пэ Зэ Дэ"},
        };

    private static readonly IReadOnlyDictionary<string, string> ReverseTranslit =
        new Dictionary<string, string>()
        {
            {"a", "а"},
            {"b", "б"},
            {"v", "в"},
            {"g", "г"},
            {"d", "д"},
            {"e", "е"},
            {"je", "ё"},
            {"zh", "ж"},
            {"z", "з"},
            {"i", "и"},
            {"y", "й"},
            {"k", "к"},
            {"l", "л"},
            {"m", "м"},
            {"n", "н"},
            {"o", "о"},
            {"p", "п"},
            {"r", "р"},
            {"s", "с"},
            {"t", "т"},
            {"u", "у"},
            {"f", "ф"},
            {"kh", "х"},
            {"c", "ц"},
            {"ch", "ч"},
            {"sh", "ш"},
            {"jsh", "щ"},
            {"hh", "ъ"},
            {"ih", "ы"},
            {"jh", "ь"},
            {"eh", "э"},
            {"ju", "ю"},
            {"ja", "я"},
        };
}

using System.Text;
using System.Text.RegularExpressions;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    private static readonly Regex UpperCaseWordRegex = new(@"\b([А-ЯЁ]{2,})\b",
        RegexOptions.Compiled);

    private static readonly Regex YesNoRegex = new(@"\b(НЕТ|ДА|нет|да)\b",
        RegexOptions.Compiled);

    private static readonly Regex NotRegex = new(@"\b(НЕ|не)\b",
        RegexOptions.Compiled);

    private static readonly Regex ImportantWordRegex = new(
        @"\b(внимание|Внимание|ВНИМАНИЕ|" +
        @"опасность|Опасность|ОПАСНОСТЬ|" +
        @"срочно|Срочно|СРОЧНО|" +
        @"важно|Важно|ВАЖНО|" +
        @"предупреждение|Предупреждение|ПРЕДУПРЕЖДЕНИЕ|" +
        @"помогите|Помогите|ПОМОГИТЕ|" +
        @"капитан|Капитан|КАПИТАН|" +
        @"командир|Командир|КОМАНДИР)\b",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(@"\s+",
        RegexOptions.Compiled);

    /// <summary>
    /// Converts text into valid SSML with pauses and emphasis.
    /// </summary>
    private string ToSsmlText(string text, SoundTraits traits = SoundTraits.None)
    {
        text = XmlEscape(text);
        text = InsertPauses(text);
        text = InsertEmphasis(text);

        var prosodyAttrs = BuildProsodyAttributes(traits);

        return !string.IsNullOrEmpty(prosodyAttrs)
            ? $"<speak><prosody {prosodyAttrs}>{text}</prosody></speak>"
            : $"<speak>{text}</speak>";
    }

    private string XmlEscape(string text)
    {
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }

    private string InsertPauses(string text)
    {
        var sb = new StringBuilder();
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (c == '!')
            {
                int count = 0;
                while (i < text.Length && text[i] == '!')
                {
                    count++;
                    i++;
                }
                sb.Append(new string('!', count));

                if (i < text.Length && text[i] == ' ' && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))
                    sb.Append(" <break time=\"400ms\"/> ");

                continue;
            }

            if (c == '?')
            {
                int count = 0;
                while (i < text.Length && text[i] == '?')
                {
                    count++;
                    i++;
                }
                sb.Append(new string('?', count));

                if (i < text.Length && text[i] == ' ' && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))
                    sb.Append(" <break time=\"400ms\"/> ");

                continue;
            }

            if (c == '?' && i + 1 < text.Length && text[i + 1] == '!')
            {
                sb.Append("?!");
                i += 2;

                if (i < text.Length && text[i] == ' ' && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))
                    sb.Append(" <break time=\"400ms\"/> ");

                continue;
            }

            if (c == '!' && i + 1 < text.Length && text[i + 1] == '?')
            {
                sb.Append("!?");
                i += 2;

                if (i < text.Length && text[i] == ' ' && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))
                    sb.Append(" <break time=\"400ms\"/> ");

                continue;
            }

            if (c == '.' && i + 2 < text.Length && text[i + 1] == '.' && text[i + 2] == '.')
            {
                sb.Append("...");
                i += 3;

                if (i < text.Length && text[i] == ' ' && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))
                    sb.Append(" <break time=\"500ms\"/> ");

                continue;
            }

            sb.Append(c);

            if (c == '.')
            {
                if (i + 1 < text.Length && char.IsLetter(text[i + 1]))
                {
                    // Nothing
                }
                else if (i + 1 < text.Length && text[i + 1] == ' ' && i + 2 < text.Length && !char.IsWhiteSpace(text[i + 2]))
                {
                    sb.Append(" <break time=\"300ms\"/> ");
                }
            }
            else if (c == ',')
            {
                if (i + 1 < text.Length && text[i + 1] == ' ')
                    sb.Append(" <break time=\"80ms\"/> ");
            }
            else if (c == ';')
            {
                if (i + 1 < text.Length && text[i + 1] == ' ')
                    sb.Append(" <break time=\"120ms\"/> ");
            }
            else if (c == ':')
            {
                if (i + 1 < text.Length && text[i + 1] == ' ')
                    sb.Append(" <break time=\"100ms\"/> ");
            }

            i++;
        }

        var result = sb.ToString();
        result = WhitespaceRegex.Replace(result, " ");
        return result.Trim();
    }

    private string InsertEmphasis(string text)
    {
        text = UpperCaseWordRegex.Replace(text, m =>
        {
            var word = m.Value.ToLowerInvariant();
            return $"<prosody pitch=\"+30%\" volume=\"+3dB\">{word}</prosody>";
        });

        text = YesNoRegex.Replace(text, m =>
        {
            var word = m.Value.ToLowerInvariant();
            return $"<prosody pitch=\"+25%\" volume=\"+6dB\">{word}</prosody>";
        });

        text = NotRegex.Replace(text, m =>
        {
            var word = m.Value.ToLowerInvariant();
            return $"<prosody pitch=\"+15%\">{word}</prosody>";
        });

        text = ImportantWordRegex.Replace(text, m =>
        {
            var word = m.Value.ToLowerInvariant();
            return $"<prosody pitch=\"+10%\">{word}</prosody>";
        });

        return text;
    }

    private string BuildProsodyAttributes(SoundTraits traits)
    {
        var attrs = new List<string>();

        if (traits.HasFlag(SoundTraits.RateXSlow))
            attrs.Add("rate=\"x-slow\"");
        else if (traits.HasFlag(SoundTraits.RateSlow))
            attrs.Add("rate=\"slow\"");
        else if (traits.HasFlag(SoundTraits.RateMedium))
            attrs.Add("rate=\"medium\"");
        else if (traits.HasFlag(SoundTraits.RateFast))
            attrs.Add("rate=\"fast\"");
        else if (traits.HasFlag(SoundTraits.RateXFast))
            attrs.Add("rate=\"x-fast\"");

        if (traits.HasFlag(SoundTraits.PitchVerylow))
            attrs.Add("pitch=\"x-low\"");
        else if (traits.HasFlag(SoundTraits.PitchLow))
            attrs.Add("pitch=\"low\"");
        else if (traits.HasFlag(SoundTraits.PitchMedium))
            attrs.Add("pitch=\"medium\"");
        else if (traits.HasFlag(SoundTraits.PitchHigh))
            attrs.Add("pitch=\"high\"");
        else if (traits.HasFlag(SoundTraits.PitchVeryhigh))
            attrs.Add("pitch=\"x-high\"");

        if (traits.HasFlag(SoundTraits.VolumeXSoft))
            attrs.Add("volume=\"x-soft\"");
        else if (traits.HasFlag(SoundTraits.VolumeSoft))
            attrs.Add("volume=\"soft\"");
        else if (traits.HasFlag(SoundTraits.VolumeMedium))
            attrs.Add("volume=\"medium\"");
        else if (traits.HasFlag(SoundTraits.VolumeLoud))
            attrs.Add("volume=\"loud\"");
        else if (traits.HasFlag(SoundTraits.VolumeXLoud))
            attrs.Add("volume=\"x-loud\"");

        return string.Join(" ", attrs);
    }

    [Flags]
    private enum SoundTraits : ushort
    {
        None = 0,

        RateXSlow = 1 << 0,
        RateSlow = 1 << 1,
        RateMedium = 1 << 2,
        RateFast = 1 << 3,
        RateXFast = 1 << 4,

        PitchVerylow = 1 << 5,
        PitchLow = 1 << 6,
        PitchMedium = 1 << 7,
        PitchHigh = 1 << 8,
        PitchVeryhigh = 1 << 9,

        VolumeXSoft = 1 << 10,
        VolumeSoft = 1 << 11,
        VolumeMedium = 1 << 12,
        VolumeLoud = 1 << 13,
        VolumeXLoud = 1 << 14,
    }
}

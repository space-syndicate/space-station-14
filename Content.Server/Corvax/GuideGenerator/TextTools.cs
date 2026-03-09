namespace Content.Server.Corvax.GuideGenerator;

public sealed class TextTools
{
    /// <summary>
    /// Capitalizes first letter of given string.
    /// </summary>
    /// <param name="str">String to capitalize</param>
    /// <returns>String with capitalized first letter</returns>
    public static string CapitalizeString(string str)
    {
        return str.Length switch
        {
            > 1 => char.ToUpper(str[0]) + str.Remove(0, 1),
            1 => char.ToUpper(str[0]).ToString(),
            _ => str
        };
    }
}

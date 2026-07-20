using System.Text;
using System.Text.RegularExpressions;

namespace CoffinTranslate.Core.Game;

/// <summary>Minimal reader for Steam's libraryfolders.vdf — only extracts library paths.</summary>
public static partial class SteamVdf
{
    [GeneratedRegex("\"path\"\\s+\"(?<path>(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex PathEntryRegex();

    public static IReadOnlyList<string> ParseLibraryPaths(string vdfContent)
    {
        var result = new List<string>();
        foreach (Match match in PathEntryRegex().Matches(vdfContent))
        {
            var path = Unescape(match.Groups["path"].Value);
            if (path.Length > 0)
                result.Add(path);
        }

        return result;
    }

    /// <summary>VDF only escapes backslash and double quote.</summary>
    private static string Unescape(string value)
    {
        if (!value.Contains('\\'))
            return value;

        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                sb.Append(value[i + 1]);
                i++;
            }
            else
            {
                sb.Append(value[i]);
            }
        }

        return sb.ToString();
    }
}

using System.Text.RegularExpressions;

namespace CoffinTranslate.Core.Project;

/// <summary>
/// The in-text formatting codes the game understands (<c>\c[n]</c> colour, <c>\fi \fb \fr</c>
/// italic/bold/reset, <c>\{ \}</c> larger/smaller). A translation must keep the same codes as its
/// source or the on-screen layout and speaker colours break.
/// </summary>
public static partial class FormattingTags
{
    [GeneratedRegex(@"\\c\[\d*\]|\\f[ibr]|\\[{}]")]
    private static partial Regex TagPattern();

    public static IReadOnlyList<string> Extract(string text) =>
        TagPattern().Matches(text).Select(m => m.Value).ToList();

    /// <summary>
    /// True when <paramref name="target"/> uses exactly the same set of codes (same kinds, same
    /// counts) as <paramref name="source"/>. An empty target is treated as consistent — it is
    /// simply not translated yet, not broken.
    /// </summary>
    public static bool Consistent(string source, string target)
    {
        if (target.Length == 0)
            return true;

        var a = Extract(source);
        var b = Extract(target);
        return a.Count == b.Count && a.Order().SequenceEqual(b.Order());
    }
}

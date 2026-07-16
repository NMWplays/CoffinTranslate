namespace CoffinTranslate.Core.Community;

/// <summary>
/// Compares loosely-formatted pack version strings like <c>"1"</c>, <c>"1.3"</c>, <c>"1.2.10"</c>.
/// Dotted numeric segments are compared numerically (so <c>1.10 &gt; 1.9</c>); anything non-numeric
/// falls back to an ordinal comparison of the whole string. Used to decide whether the catalog offers
/// a newer version than what's installed.
/// </summary>
public static class CommunityVersion
{
    public static int Compare(string a, string b)
    {
        a = a.Trim();
        b = b.Trim();

        var pa = a.Split('.');
        var pb = b.Split('.');
        int len = Math.Max(pa.Length, pb.Length);

        for (int i = 0; i < len; i++)
        {
            var sa = i < pa.Length ? pa[i] : "0";
            var sb = i < pb.Length ? pb[i] : "0";

            if (int.TryParse(sa, out int na) && int.TryParse(sb, out int nb))
            {
                if (na != nb)
                    return na < nb ? -1 : 1;
            }
            else
            {
                // a non-numeric segment (e.g. "1.3b") — compare the whole strings and stop
                return string.CompareOrdinal(a, b) switch { < 0 => -1, > 0 => 1, _ => 0 };
            }
        }

        return 0;
    }

    /// <summary>True when <paramref name="available"/> is a strictly newer version than <paramref name="installed"/>.</summary>
    public static bool IsNewer(string available, string installed) => Compare(available, installed) > 0;
}

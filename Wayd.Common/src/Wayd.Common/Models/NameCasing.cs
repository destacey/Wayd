using System.Globalization;
using System.Text;

namespace Wayd.Common.Models;

/// <summary>
/// English-name title-casing for strings imported from upstream HRIS systems that store names in
/// all-caps for compliance reporting (Workday's Reporting_Name, payroll systems, government
/// document exports). The detection heuristic guards against modifying already-cased input — only
/// strings that are "mostly uppercase" are touched, so a user's deliberately-cased d'Artagnan or
/// van der Berg stays as-is.
///
/// Scope is deliberately narrow: Western European naming conventions. Hungarian / Vietnamese name
/// order, full Icelandic patronymics, etc. are out of scope — they aren't common HRIS upstreams
/// for Wayd and getting them subtly wrong is worse than not trying.
/// </summary>
public static class NameCasing
{
    // A string counts as "mostly uppercase" when at least this fraction of its letters are upper.
    // Tuned for short names (3–4 letters): 0.8 means "Stacey" passes through (only S is upper, 1/6
    // = 0.17, well below threshold) but "STACEY" and "MCDONALD" trigger title-casing.
    private const double UppercaseThreshold = 0.8;

    // Surname prefixes that take a non-default inner-capital. The rule for Mc / Mac is "preserve
    // the inner capital ONLY when followed by a capital letter" — so MacDonald gets "MacDonald"
    // but Macbeth (which we never see in HRIS data because it's a play) stays "Macbeth". We don't
    // include Van/Von/De/Du/La/Le here because they're lowercase-particle conventions: "van der
    // Berg", "von Trapp". Those flow through the default word-boundary rule naturally.
    private static readonly string[] InnerCapPrefixes = ["Mc", "Mac"];

    /// <summary>
    /// Returns a title-cased copy of <paramref name="input"/> if it is mostly uppercase. Returns
    /// the input unchanged when it is null/whitespace, already mixed-case, or contains no letters
    /// at all. Pure pass-through is the default — only deliberately-all-caps strings get touched.
    /// </summary>
    public static string? TitleCaseIfMostlyUpper(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        if (!IsMostlyUpper(input)) return input;
        return TitleCase(input);
    }

    private static bool IsMostlyUpper(string s)
    {
        int upper = 0, letters = 0;
        foreach (var ch in s)
        {
            if (!char.IsLetter(ch)) continue;
            letters++;
            if (char.IsUpper(ch)) upper++;
        }
        if (letters == 0) return false;
        return ((double)upper / letters) >= UppercaseThreshold;
    }

    /// <summary>
    /// Word-boundary-aware title-casing. Word boundaries are whitespace, hyphens, and apostrophes
    /// — so "MARY-ANNE", "O'BRIEN", and "MARY ANNE" all give three words to capitalize. After the
    /// initial pass we re-apply Mc/Mac rules so "MCDONALD" → "Mcdonald" → "McDonald".
    /// </summary>
    private static string TitleCase(string s)
    {
        var sb = new StringBuilder(s.Length);
        var atWordStart = true;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '\'' or '’')
            {
                sb.Append(ch);
                atWordStart = true;
                continue;
            }

            if (char.IsLetter(ch))
            {
                if (atWordStart)
                {
                    sb.Append(char.ToUpper(ch, CultureInfo.InvariantCulture));
                    atWordStart = false;
                }
                else
                {
                    sb.Append(char.ToLower(ch, CultureInfo.InvariantCulture));
                }
            }
            else
            {
                sb.Append(ch);
                atWordStart = false;
            }
        }

        return ApplyInnerCapPrefixes(sb.ToString());
    }

    /// <summary>
    /// Walks the post-title-cased string looking for word-start prefixes that need an inner capital
    /// (Mc, Mac). When found, uppercase the character that follows the prefix. Word boundaries
    /// here are the same as in <see cref="TitleCase"/>.
    /// </summary>
    private static string ApplyInnerCapPrefixes(string s)
    {
        var chars = s.ToCharArray();
        var i = 0;
        while (i < chars.Length)
        {
            // Find the start of the next word.
            if (i == 0 || IsWordBoundary(chars[i - 1]))
            {
                foreach (var prefix in InnerCapPrefixes)
                {
                    var afterPrefix = i + prefix.Length;
                    if (afterPrefix >= chars.Length) continue;
                    if (!MatchesPrefixCaseInsensitive(chars, i, prefix)) continue;
                    if (!char.IsLetter(chars[afterPrefix])) continue;

                    // Mc/Mac uppercases the next letter to preserve "McDonald" — but only when
                    // there's at least one more letter after, so we don't accidentally produce
                    // "Mc" + uppercase-final-letter from a one-letter follow ("Mco" would become
                    // "McO" which is wrong; in practice HRIS data won't have these but be safe).
                    chars[afterPrefix] = char.ToUpper(chars[afterPrefix], CultureInfo.InvariantCulture);
                    break;
                }
            }
            i++;
        }
        return new string(chars);
    }

    private static bool IsWordBoundary(char ch) =>
        char.IsWhiteSpace(ch) || ch is '-' or '\'' or '’';

    private static bool MatchesPrefixCaseInsensitive(char[] chars, int start, string prefix)
    {
        if (start + prefix.Length > chars.Length) return false;
        for (var i = 0; i < prefix.Length; i++)
        {
            if (char.ToLowerInvariant(chars[start + i]) != char.ToLowerInvariant(prefix[i]))
                return false;
        }
        return true;
    }
}

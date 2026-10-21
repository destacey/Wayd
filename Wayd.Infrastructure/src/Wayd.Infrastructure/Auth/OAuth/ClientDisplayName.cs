using System.Globalization;
using System.Text;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// Makes a client's name for itself safe to show a person deciding whether to trust it. The name is the client's own
/// claim, so it must not be able to reorder the text around it, hide characters, spill over neighbouring text, or push
/// the client's real host out of view.
/// </summary>
internal static class ClientDisplayName
{
    /// <summary>The most text elements kept, so a long name cannot crowd out the host shown beside it.</summary>
    internal const int MaxLength = 60;

    /// <summary>The most UTF-16 characters kept, well inside the 200 the name's columns hold.</summary>
    internal const int MaxChars = 120;

    /// <summary>The most combining marks kept on one character; more only stack into text that spills over its neighbours.</summary>
    internal const int MaxCombiningMarks = 2;

    /// <summary>
    /// Removes control, format (bidi overrides, zero-width characters) and separator characters other than an
    /// ordinary space, collapses runs of whitespace, limits stacked combining marks, and caps the length. Answers
    /// <paramref name="fallback"/>, capped the same way, when nothing is left.
    /// </summary>
    internal static string Clean(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Cap(fallback);

        var builder = new StringBuilder(name.Length);
        var pendingSpace = false;
        var marks = 0;
        foreach (var rune in name.Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned)
            {
                continue;
            }

            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                if (builder.Length == 0 || pendingSpace || ++marks > MaxCombiningMarks)
                    continue;
            }
            else
            {
                marks = 0;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(rune.ToString());
        }

        return builder.Length == 0 ? Cap(fallback) : Cap(builder.ToString());
    }

    /// <summary>
    /// Shortens <paramref name="text"/> to <see cref="MaxLength"/> text elements and <see cref="MaxChars"/> characters,
    /// cutting only between text elements and marking the cut.
    /// </summary>
    private static string Cap(string text)
    {
        var elements = new StringInfo(text);
        if (elements.LengthInTextElements <= MaxLength && text.Length <= MaxChars)
            return text;

        var kept = new StringBuilder();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var count = 0;
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (++count > MaxLength - 1 || kept.Length + element.Length > MaxChars - 1)
                break;
            kept.Append(element);
        }

        return kept.ToString().TrimEnd() + "…";
    }
}

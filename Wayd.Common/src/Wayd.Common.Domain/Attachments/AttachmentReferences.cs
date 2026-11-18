using System.Text.RegularExpressions;

namespace Wayd.Common.Domain.Attachments;

/// <summary>
/// How markdown refers to an attachment: <c>/api/attachments/{id}</c>, a path relative to the API, written as an
/// image destination.
/// </summary>
/// <remarks>
/// Any occurrence of the path counts as a reference, inside a code block or not. Counting too many only keeps an
/// image alive a little longer; counting too few would delete one a record still shows.
/// </remarks>
public static partial class AttachmentReferences
{
    /// <summary>The path an attachment's markdown reference carries.</summary>
    public static string PathFor(Guid id) => $"/api/attachments/{id:D}";

    /// <summary>The ids of every attachment the texts refer to, each once, in the order they first appear.</summary>
    public static IReadOnlyList<Guid> In(params IEnumerable<string?> markdown)
    {
        var ids = new List<Guid>();
        var seen = new HashSet<Guid>();
        foreach (var text in markdown)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (Match match in ReferencePattern().Matches(text))
            {
                var id = Guid.Parse(match.Groups["id"].Value);
                if (seen.Add(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// The text with each reference to an attachment in <paramref name="replacements"/> pointed at its replacement.
    /// </summary>
    public static string? Replace(string? markdown, IReadOnlyDictionary<Guid, Guid> replacements)
    {
        if (string.IsNullOrEmpty(markdown) || replacements.Count == 0)
        {
            return markdown;
        }

        return ReferencePattern().Replace(markdown, match =>
            replacements.TryGetValue(Guid.Parse(match.Groups["id"].Value), out var replacement)
                ? PathFor(replacement)
                : match.Value);
    }

    // Case-insensitive so a reference typed by hand in upper case still counts as one.
    [GeneratedRegex(@"/api/attachments/(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?![0-9a-z-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
}

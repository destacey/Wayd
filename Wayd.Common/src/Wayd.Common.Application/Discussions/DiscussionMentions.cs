using System.Text.RegularExpressions;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// Reads the mentions a message's markdown carries.
/// </summary>
/// <remarks>
/// A mention is written <c>@[Name](employee:{id})</c>: the id is what is stored and notified, and the name is only
/// what the author saw. Readers are shown the person's current name, so a renamed employee never leaves a stale one
/// behind.
/// </remarks>
public static partial class DiscussionMentions
{
    /// <summary>The employee ids a message mentions, distinct, in the order they first appear.</summary>
    public static IReadOnlyList<Guid> Parse(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return [];
        }

        return [.. MentionToken().Matches(body)
            .Select(m => Guid.TryParse(m.Groups["id"].Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()];
    }

    [GeneratedRegex(@"@\[[^\]\r\n]{1,200}\]\(employee:(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)")]
    private static partial Regex MentionToken();
}

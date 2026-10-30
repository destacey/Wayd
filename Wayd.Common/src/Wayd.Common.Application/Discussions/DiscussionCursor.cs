using System.Globalization;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// Where a page of a conversation ends: the last top-level message's posting instant, then its id, the keys the
/// conversation is ordered by in either direction.
/// </summary>
/// <remarks>
/// Ids break ties between messages posted at the same instant, so the position is exact. Activity between messages
/// is placed by instant alone, so the cursor never names an activity entry.
/// </remarks>
/// <param name="PostedOn">When the page's last message was posted.</param>
/// <param name="MessageId">The page's last message.</param>
public readonly record struct DiscussionCursor(Instant PostedOn, Guid MessageId) : ICursor<DiscussionCursor>
{
    /// <summary>
    /// The earliest and latest instants a cursor may hold: those SQL Server's <c>datetime2</c> can be compared with.
    /// </summary>
    private static readonly long MinTicks = Instant.FromDateTimeUtc(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)).ToUnixTimeTicks();

    private static readonly long MaxTicks = Instant.FromDateTimeUtc(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)).ToUnixTimeTicks();

    /// <inheritdoc />
    public string Encode() => CursorEncoding.Encode(
        PostedOn.ToUnixTimeTicks().ToString(CultureInfo.InvariantCulture),
        MessageId.ToString("N", CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public static bool TryDecode(string? value, out DiscussionCursor cursor)
    {
        cursor = default;
        var parts = CursorEncoding.Decode(value, 2);
        if (parts is null
            || !long.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ticks)
            || ticks < MinTicks
            || ticks > MaxTicks
            || !Guid.TryParseExact(parts[1], "N", out var messageId))
        {
            return false;
        }

        cursor = new DiscussionCursor(Instant.FromUnixTimeTicks(ticks), messageId);
        return true;
    }
}

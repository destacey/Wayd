using System.Globalization;

namespace Wayd.Common.Application.Notifications;

/// <summary>
/// Where a page of the notification list ends: the last entry's newest change, then its record, the keys the
/// list is ordered by.
/// </summary>
/// <remarks>
/// Entries changed at the same instant are ordered by their record, which never changes, rather than by a
/// notification id, which a late-arriving notification would raise and so move an entry the reader has not
/// reached yet back past the cursor. Owner type and id together are unique, so the position is exact.
/// </remarks>
/// <param name="LatestOn">When the entry's newest change happened.</param>
/// <param name="OwnerType">The entry's record's owner type.</param>
/// <param name="OwnerId">The entry's record's id.</param>
public readonly record struct NotificationCursor(Instant LatestOn, string OwnerType, Guid OwnerId)
{
    /// <summary>
    /// The earliest and latest instants a cursor may hold: those SQL Server's <c>datetime2</c> can be compared
    /// with, since the instant is passed to the database through <see cref="DateTime"/>.
    /// </summary>
    private static readonly long MinTicks = Instant.FromDateTimeUtc(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)).ToUnixTimeTicks();

    private static readonly long MaxTicks = Instant.FromDateTimeUtc(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)).ToUnixTimeTicks();

    /// <summary>The longest owner type a cursor may name; registered owner types are far shorter.</summary>
    private const int MaxOwnerTypeLength = 64;

    /// <summary>The cursor as the API hands it out.</summary>
    public string Encode() => CursorEncoding.Encode(
        LatestOn.ToUnixTimeTicks().ToString(CultureInfo.InvariantCulture),
        OwnerType,
        OwnerId.ToString("N", CultureInfo.InvariantCulture));

    /// <summary>Reads a cursor the API handed out, refusing anything that cannot be one.</summary>
    public static bool TryDecode(string? value, out NotificationCursor cursor)
    {
        cursor = default;
        var parts = CursorEncoding.Decode(value, 3);
        if (parts is null
            || !long.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ticks)
            || ticks < MinTicks
            || ticks > MaxTicks
            || string.IsNullOrWhiteSpace(parts[1])
            || parts[1].Length > MaxOwnerTypeLength
            || !Guid.TryParseExact(parts[2], "N", out var ownerId))
        {
            return false;
        }

        cursor = new NotificationCursor(Instant.FromUnixTimeTicks(ticks), parts[1], ownerId);
        return true;
    }
}

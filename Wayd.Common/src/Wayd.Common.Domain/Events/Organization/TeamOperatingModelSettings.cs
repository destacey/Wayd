using NodaTime;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// How a team works under one operating model, as the team operating model events record it.
/// </summary>
/// <param name="TimeZone">The IANA id of the time zone the team's days are counted in.</param>
/// <param name="CommitmentGraceDays">How many days after a sprint's planned start its commitment is taken.</param>
/// <param name="WorkingDays">
/// The days of the week the team works, Monday first. Null on payloads recorded before version 1.1, when every
/// operating model worked Monday to Friday.
/// </param>
/// <param name="HolidayCalendarId">
/// The holiday calendar the team takes off, or null for the system default calendar. Null on payloads recorded
/// before version 1.2, when there were no holiday calendars.
/// </param>
public sealed record TeamOperatingModelSettings(
    Methodology Methodology,
    SizingMethod SizingMethod,
    string TimeZone,
    int CommitmentGraceDays,
    IReadOnlyList<IsoDayOfWeek>? WorkingDays = null,
    Guid? HolidayCalendarId = null)
{
    /// <summary>Compares the working days by value, so a correction that changes nothing raises nothing.</summary>
    public bool Equals(TeamOperatingModelSettings? other) =>
        other is not null
        && Methodology == other.Methodology
        && SizingMethod == other.SizingMethod
        && TimeZone == other.TimeZone
        && CommitmentGraceDays == other.CommitmentGraceDays
        && HolidayCalendarId == other.HolidayCalendarId
        && (WorkingDays is null ? other.WorkingDays is null : other.WorkingDays is not null && WorkingDays.SequenceEqual(other.WorkingDays));

    public override int GetHashCode() =>
        HashCode.Combine(Methodology, SizingMethod, TimeZone, CommitmentGraceDays, WorkingDays?.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)), HolidayCalendarId);
}

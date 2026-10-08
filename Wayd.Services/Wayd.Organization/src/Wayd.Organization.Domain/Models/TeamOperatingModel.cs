using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// Represents the operating model for a team, defining how the team works (methodology, sizing method, time
/// zone, commitment grace period, working week and holiday calendar) for a specific date range.
/// </summary>
/// <remarks>
/// <see cref="Team.CorrectOperatingModel"/> edits a model in place and so corrects its whole period.
/// </remarks>
public sealed class TeamOperatingModel : OperatingModel
{
    private TeamOperatingModel() { }

    private TeamOperatingModel(OperatingModelDateRange dateRange, Methodology methodology, SizingMethod sizingMethod, string timeZone, int commitmentGraceDays, WorkingWeek workingWeek, Guid? holidayCalendarId)
        : base(dateRange, timeZone)
    {
        Methodology = methodology;
        SizingMethod = sizingMethod;
        CommitmentGraceDays = commitmentGraceDays;
        WorkingWeek = workingWeek;
        HolidayCalendarId = holidayCalendarId;
    }

    /// <summary>Gets the methodology the team uses.</summary>
    public Methodology Methodology { get; private set; }

    /// <summary>Gets the sizing method the team uses.</summary>
    public SizingMethod SizingMethod { get; private set; }

    /// <summary>
    /// Gets how many days after a sprint's planned start its commitment is taken, when the team does not
    /// start it: 1 is the end of the first planned day.
    /// </summary>
    public int CommitmentGraceDays { get; private set; }

    /// <summary>Gets the days of the week the team works.</summary>
    public WorkingWeek WorkingWeek { get; private set; } = WorkingWeek.MondayToFriday;

    /// <summary>
    /// Gets the holiday calendar whose holidays the team takes off, or null for the system default calendar, read
    /// when the schedule is read so changing the default changes every model that has none.
    /// </summary>
    public Guid? HolidayCalendarId { get; private set; }

    /// <summary>
    /// Corrects this operating model for its whole period.
    /// </summary>
    /// <param name="methodology">The new methodology.</param>
    /// <param name="sizingMethod">The new sizing method.</param>
    /// <param name="timeZone">The IANA id of the team's time zone.</param>
    /// <param name="commitmentGraceDays">The commitment grace period in days.</param>
    /// <param name="workingWeek">The days of the week the team works.</param>
    /// <param name="holidayCalendarId">The team's holiday calendar, or null for the system default.</param>
    /// <returns>A result indicating success or failure.</returns>
    internal Result Update(Methodology methodology, SizingMethod sizingMethod, string timeZone, int commitmentGraceDays, WorkingWeek workingWeek, Guid? holidayCalendarId)
    {
        ArgumentNullException.ThrowIfNull(workingWeek);

        var scheduleResult = ValidateSchedule(timeZone, commitmentGraceDays);
        if (scheduleResult.IsFailure)
            return scheduleResult;

        Methodology = methodology;
        SizingMethod = sizingMethod;
        TimeZone = timeZone;
        CommitmentGraceDays = commitmentGraceDays;
        WorkingWeek = workingWeek;
        HolidayCalendarId = holidayCalendarId;
        return Result.Success();
    }

    /// <summary>
    /// Creates a new team operating model.
    /// If a current model exists, it will be closed with an end date of one day before the new model's start date.
    /// </summary>
    /// <param name="startDate">The start date for this operating model.</param>
    /// <param name="methodology">The methodology the team uses.</param>
    /// <param name="sizingMethod">The sizing method the team uses.</param>
    /// <param name="timeZone">The IANA id of the team's time zone.</param>
    /// <param name="commitmentGraceDays">The commitment grace period in days.</param>
    /// <param name="workingWeek">The days of the week the team works.</param>
    /// <param name="holidayCalendarId">The team's holiday calendar, or null for the system default.</param>
    /// <param name="currentModel">The current operating model, if one exists.</param>
    /// <returns>A result containing the new operating model or an error.</returns>
    internal static Result<TeamOperatingModel> Create(
        LocalDate startDate,
        Methodology methodology,
        SizingMethod sizingMethod,
        string timeZone,
        int commitmentGraceDays,
        WorkingWeek workingWeek,
        Guid? holidayCalendarId,
        TeamOperatingModel? currentModel = null)
    {
        ArgumentNullException.ThrowIfNull(workingWeek);

        var scheduleResult = ValidateSchedule(timeZone, commitmentGraceDays);
        if (scheduleResult.IsFailure)
            return Result.Failure<TeamOperatingModel>(scheduleResult.Error);

        var dateRange = OpenFrom(startDate, currentModel);
        if (dateRange.IsFailure)
            return Result.Failure<TeamOperatingModel>(dateRange.Error);

        return new TeamOperatingModel(dateRange.Value, methodology, sizingMethod, timeZone, commitmentGraceDays, workingWeek, holidayCalendarId);
    }

    private static Result ValidateSchedule(string timeZone, int commitmentGraceDays)
    {
        var timeZoneResult = ValidateTimeZone(timeZone);
        if (timeZoneResult.IsFailure)
            return timeZoneResult;

        if (commitmentGraceDays < 0)
            return Result.Failure("The commitment grace period cannot be negative.");

        return Result.Success();
    }
}

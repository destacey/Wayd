using CSharpFunctionalExtensions;
using Wayd.Organization.Domain.Enums;
using NodaTime;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// Represents the operating model for a team, defining how the team works
/// (methodology, sizing method, time zone and commitment grace period) for a specific date range.
/// </summary>
/// <remarks>
/// A team that changes how it works opens a new model, so history before the change keeps the old values;
/// <see cref="Update"/> edits a model in place and so corrects its whole period.
/// </remarks>
public sealed class TeamOperatingModel : BaseAuditableEntity
{
    private TeamOperatingModel() { }

    private TeamOperatingModel(OperatingModelDateRange dateRange, Methodology methodology, SizingMethod sizingMethod, string timeZone, int commitmentGraceDays)
    {
        DateRange = dateRange;
        Methodology = methodology;
        SizingMethod = sizingMethod;
        TimeZone = timeZone;
        CommitmentGraceDays = commitmentGraceDays;
    }

    /// <summary>Gets the effective date range for this operating model.</summary>
    public OperatingModelDateRange DateRange { get; private set; } = null!;

    /// <summary>Gets the methodology the team uses.</summary>
    public Methodology Methodology { get; private set; }

    /// <summary>Gets the sizing method the team uses.</summary>
    public SizingMethod SizingMethod { get; private set; }

    /// <summary>Gets the IANA id of the time zone the team's days are counted in.</summary>
    public string TimeZone { get; private set; } = null!;

    /// <summary>
    /// Gets how many days after a sprint's planned start its commitment is taken, when the team does not
    /// start it: 1 is the end of the first planned day.
    /// </summary>
    public int CommitmentGraceDays { get; private set; }

    /// <summary>Gets whether this operating model is current (has no end date).</summary>
    public bool IsCurrent => DateRange.IsCurrent;

    /// <summary>
    /// Corrects this operating model for its whole period.
    /// </summary>
    /// <param name="methodology">The new methodology.</param>
    /// <param name="sizingMethod">The new sizing method.</param>
    /// <param name="timeZone">The IANA id of the team's time zone.</param>
    /// <param name="commitmentGraceDays">The commitment grace period in days.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result Update(Methodology methodology, SizingMethod sizingMethod, string timeZone, int commitmentGraceDays)
    {
        var scheduleResult = ValidateSchedule(timeZone, commitmentGraceDays);
        if (scheduleResult.IsFailure)
            return scheduleResult;

        Methodology = methodology;
        SizingMethod = sizingMethod;
        TimeZone = timeZone;
        CommitmentGraceDays = commitmentGraceDays;
        return Result.Success();
    }

    /// <summary>
    /// Closes this operating model by setting its end date.
    /// </summary>
    /// <param name="endDate">The end date for this operating model.</param>
    internal void Close(LocalDate endDate)
    {
        DateRange.SetEnd(endDate);
    }

    /// <summary>
    /// Clears the end date from the current date range, leaving only the start date set.
    /// </summary>
    internal void ClearEndDate()
    {
        DateRange.ClearEnd();
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
    /// <param name="currentModel">The current operating model, if one exists.</param>
    /// <returns>A result containing the new operating model or an error.</returns>
    internal static Result<TeamOperatingModel> Create(
        LocalDate startDate,
        Methodology methodology,
        SizingMethod sizingMethod,
        string timeZone,
        int commitmentGraceDays,
        TeamOperatingModel? currentModel = null)
    {
        var scheduleResult = ValidateSchedule(timeZone, commitmentGraceDays);
        if (scheduleResult.IsFailure)
            return Result.Failure<TeamOperatingModel>(scheduleResult.Error);

        // If there's a current model, validate and close it
        if (currentModel is not null && currentModel.IsCurrent)
        {
            // The new model's start date must be after the current model's start date
            if (startDate <= currentModel.DateRange.Start)
            {
                return Result.Failure<TeamOperatingModel>(
                    "New operating model start date must be after the current model's start date.");
            }

            // Close the current model one day before the new one starts
            var previousEndDate = startDate.PlusDays(-1);
            currentModel.Close(previousEndDate);
        }

        var dateRange = new OperatingModelDateRange(startDate, null);
        var model = new TeamOperatingModel(dateRange, methodology, sizingMethod, timeZone, commitmentGraceDays);

        return Result.Success(model);
    }

    private static Result ValidateSchedule(string timeZone, int commitmentGraceDays)
    {
        // Stored zones are resolved against NodaTime's bundled Tzdb, so accept only ids it knows.
        if (string.IsNullOrWhiteSpace(timeZone) || DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone) is null)
            return Result.Failure($"'{timeZone}' is not a valid IANA time zone.");

        if (commitmentGraceDays < 0)
            return Result.Failure("The commitment grace period cannot be negative.");

        return Result.Success();
    }
}

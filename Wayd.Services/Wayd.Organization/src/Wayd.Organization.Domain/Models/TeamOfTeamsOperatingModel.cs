using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// How a team of teams works for a specific date range: the time zone its own rollups count days in.
/// </summary>
/// <remarks>
/// Methodology and sizing belong to the child teams, which can mix them. The zone is never inherited by a
/// child team; it only pre-fills a child's new operating model.
/// <see cref="TeamOfTeams.CorrectOperatingModel"/> edits a model in place and so corrects its whole period.
/// </remarks>
public sealed class TeamOfTeamsOperatingModel : OperatingModel
{
    private TeamOfTeamsOperatingModel() { }

    private TeamOfTeamsOperatingModel(OperatingModelDateRange dateRange, string timeZone)
        : base(dateRange, timeZone)
    {
    }

    /// <summary>
    /// Corrects this operating model for its whole period.
    /// </summary>
    /// <param name="timeZone">The IANA id of the team of teams' time zone.</param>
    /// <returns>A result indicating success or failure.</returns>
    internal Result Update(string timeZone)
    {
        var timeZoneResult = ValidateTimeZone(timeZone);
        if (timeZoneResult.IsFailure)
            return timeZoneResult;

        TimeZone = timeZone;
        return Result.Success();
    }

    /// <summary>
    /// Creates a new team of teams operating model, closing <paramref name="currentModel"/> the day before
    /// <paramref name="startDate"/>.
    /// </summary>
    /// <param name="startDate">The start date for this operating model.</param>
    /// <param name="timeZone">The IANA id of the team of teams' time zone.</param>
    /// <param name="currentModel">The current operating model, if one exists.</param>
    /// <returns>A result containing the new operating model or an error.</returns>
    internal static Result<TeamOfTeamsOperatingModel> Create(LocalDate startDate, string timeZone, TeamOfTeamsOperatingModel? currentModel = null)
    {
        var timeZoneResult = ValidateTimeZone(timeZone);
        if (timeZoneResult.IsFailure)
            return Result.Failure<TeamOfTeamsOperatingModel>(timeZoneResult.Error);

        var dateRange = OpenFrom(startDate, currentModel);
        if (dateRange.IsFailure)
            return Result.Failure<TeamOfTeamsOperatingModel>(dateRange.Error);

        return new TeamOfTeamsOperatingModel(dateRange.Value, timeZone);
    }
}

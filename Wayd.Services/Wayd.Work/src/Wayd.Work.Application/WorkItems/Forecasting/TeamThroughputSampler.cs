using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Application.WorkItems.Forecasting;

/// <summary>
/// Builds each team's throughput sample from the Requirement-tier items it finished in a window.
/// </summary>
/// <remarks>
/// Completion instants become calendar dates in UTC; Wayd does not yet track a time zone for
/// users, teams or the organization.
/// </remarks>
internal sealed class TeamThroughputSampler(IWorkDbContext workDbContext)
{
    /// <summary>
    /// Fewer completions than this in the lookback window is too little history to forecast from.
    /// </summary>
    public const int MinimumItemsCompleted = 10;

    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <summary>
    /// The <paramref name="days"/> whole UTC days before <paramref name="now"/>. Today is left
    /// out because it is not over, and would read as a slow day.
    /// </summary>
    public static (LocalDate From, LocalDate To) LookbackWindow(Instant now, int days = ForecastOptions.DefaultLookbackDays)
    {
        Guard.Against.NegativeOrZero(days);

        var to = now.InUtc().Date.PlusDays(-1);
        return (to.PlusDays(1 - days), to);
    }

    public static bool HasEnoughHistory(ThroughputSample sample) => sample.Total >= MinimumItemsCompleted;

    /// <returns>A sample for every team asked for, including teams that finished nothing.</returns>
    public async Task<Dictionary<Guid, ThroughputSample>> Sample(
        IReadOnlyCollection<Guid> teamIds,
        LocalDate from,
        LocalDate to,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(teamIds);

        var start = from.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
        var end = to.PlusDays(1).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();

        // Removed items also carry a DoneTimestamp, so the status category is what excludes them.
        var completions = await _workDbContext.WorkItems
            .Where(w => w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value))
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => w.StatusCategory == WorkStatusCategory.Done)
            .Where(w => w.DoneTimestamp >= start && w.DoneTimestamp < end)
            .Select(w => new { TeamId = w.TeamId!.Value, DoneTimestamp = w.DoneTimestamp!.Value })
            .ToListAsync(cancellationToken);

        var completedOnByTeam = completions.ToLookup(c => c.TeamId, c => c.DoneTimestamp.InUtc().Date);

        return teamIds.Distinct().ToDictionary(
            teamId => teamId,
            teamId => ThroughputSample.FromCompletions(completedOnByTeam[teamId], from, to));
    }
}

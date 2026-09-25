using Wayd.Common.Application.Models.Organizations;
using Wayd.Web.Api.Extensions;
using Wayd.Work.Application.WorkTeams.Allocation;
using Wayd.Work.Application.WorkTeams.Queries;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// An allocation report for a team or a team of teams. Only the dates are required.
/// </summary>
public sealed record GetTeamAllocationRequest
{
    /// <summary>The first day of completed work to include (yyyy-MM-dd, UTC).</summary>
    public string? From { get; set; }

    /// <summary>The last day of completed work to include (yyyy-MM-dd, UTC).</summary>
    public string? To { get; set; }

    /// <summary>What to group work by (default Portfolio).</summary>
    public AllocationDimension? Dimension { get; set; }

    /// <summary>How to weigh each work item (default Count).</summary>
    public AllocationMeasure? Measure { get; set; }

    /// <summary>Story points only: what to do with unestimated items (default Exclude).</summary>
    public UnestimatedHandling? Unestimated { get; set; }

    /// <summary>Strategic theme only: how to credit a project with several themes (default SplitEvenly).</summary>
    public ThemeCounting? ThemeCounting { get; set; }

    /// <returns>False, with <paramref name="error"/> set, when a date is missing or malformed.</returns>
    public bool TryToQuery(TeamIdOrCode teamIdOrCode, out GetTeamAllocationQuery? query, out string? error)
    {
        query = null;
        error = null;

        if (!IsoDateQuery.TryParse(From, out var from) || !IsoDateQuery.TryParse(To, out var to))
        {
            error = IsoDateQuery.FormatError;
            return false;
        }

        if (from is null || to is null)
        {
            error = "Both from and to dates are required.";
            return false;
        }

        query = new GetTeamAllocationQuery(
            teamIdOrCode,
            from.Value,
            to.Value,
            new AllocationOptions(
                Dimension ?? AllocationDimension.Portfolio,
                Measure ?? AllocationMeasure.Count,
                Unestimated ?? UnestimatedHandling.Exclude,
                ThemeCounting ?? Wayd.Work.Application.WorkTeams.Allocation.ThemeCounting.SplitEvenly));
        return true;
    }
}

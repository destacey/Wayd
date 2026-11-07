using Wayd.Common.Application.Models.Organizations;
using Wayd.Web.Api.Extensions;
using Wayd.Work.Application.WorkTeams.Queries;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// A dependency analysis report for a team or a team of teams, over a date range.
/// </summary>
public sealed record GetTeamDependencyAnalysisRequest
{
    /// <summary>The first day of the range (yyyy-MM-dd, in the team's time zone).</summary>
    public string? From { get; set; }

    /// <summary>The last day of the range (yyyy-MM-dd, in the team's time zone).</summary>
    public string? To { get; set; }

    /// <returns>False, with <paramref name="error"/> set, when a date is missing or malformed.</returns>
    public bool TryToQuery(TeamIdOrCode teamIdOrCode, out GetTeamDependencyAnalysisQuery? query, out string? error)
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

        query = new GetTeamDependencyAnalysisQuery(teamIdOrCode, from.Value, to.Value);
        return true;
    }
}

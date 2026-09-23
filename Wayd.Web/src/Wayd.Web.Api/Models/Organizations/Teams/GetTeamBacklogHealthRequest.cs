using Wayd.Common.Application.Models.Organizations;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.Domain.Models.BacklogHealth;

namespace Wayd.Web.Api.Models.Organizations.Teams;

/// <summary>
/// Optional overrides for a backlog health report. Anything left out uses its default.
/// </summary>
public sealed record GetTeamBacklogHealthRequest
{
    /// <summary>Days of history to measure throughput, cycle time and net flow over (14-365, default 90).</summary>
    public int? LookbackDays { get; set; }

    /// <summary>Days without a change before a work item is stale (default 90).</summary>
    public int? StaleDays { get; set; }

    /// <summary>Days since creation before a proposed work item is old (default 180).</summary>
    public int? OldProposedDays { get; set; }

    /// <summary>The cycle time percentile an active work item is aging beyond (default 85).</summary>
    public int? AgingWipPercentile { get; set; }

    /// <summary>The story point percentile a work item is oversized above (default 85).</summary>
    public int? OversizedPercentile { get; set; }

    /// <summary>Weeks of throughput the readiness checks look ahead (default 4).</summary>
    public int? ReadinessWindowWeeks { get; set; }

    /// <summary>Top-ranked work items the readiness checks look at without enough history (default 20).</summary>
    public int? ReadinessFallbackItems { get; set; }

    /// <summary>Percent of work items flagged at which a check is At Risk (default 10).</summary>
    public int? AtRiskPercent { get; set; }

    /// <summary>Percent of work items flagged at which a check is Unhealthy (default 25).</summary>
    public int? UnhealthyPercent { get; set; }

    /// <summary>Runway weeks below which the backlog is At Risk (default 4).</summary>
    public double? RunwayAtRiskWeeks { get; set; }

    /// <summary>Runway weeks below which the backlog is Unhealthy (default 2).</summary>
    public double? RunwayUnhealthyWeeks { get; set; }

    /// <summary>Runway weeks above which the backlog is At Risk for being too long (default 26).</summary>
    public double? RunwayTooLongWeeks { get; set; }

    /// <summary>Work items created per item completed above which net flow is At Risk (default 1.2).</summary>
    public double? NetFlowAtRisk { get; set; }

    /// <summary>Work items created per item completed above which net flow is Unhealthy (default 1.5).</summary>
    public double? NetFlowUnhealthy { get; set; }

    /// <summary>Active work items per member above which WIP load is At Risk (default 1.5).</summary>
    public double? WipLoadAtRisk { get; set; }

    /// <summary>Active work items per member above which WIP load is Unhealthy (default 2).</summary>
    public double? WipLoadUnhealthy { get; set; }

    public GetTeamBacklogHealthQuery ToGetTeamBacklogHealthQuery(TeamIdOrCode teamIdOrCode)
    {
        var defaults = BacklogHealthThresholds.Default;
        var thresholds = new BacklogHealthThresholds
        {
            StaleDays = StaleDays ?? defaults.StaleDays,
            OldProposedDays = OldProposedDays ?? defaults.OldProposedDays,
            AgingWipPercentile = AgingWipPercentile ?? defaults.AgingWipPercentile,
            OversizedPercentile = OversizedPercentile ?? defaults.OversizedPercentile,
            ReadinessWindowWeeks = ReadinessWindowWeeks ?? defaults.ReadinessWindowWeeks,
            ReadinessFallbackItems = ReadinessFallbackItems ?? defaults.ReadinessFallbackItems,
            AtRiskPercent = AtRiskPercent ?? defaults.AtRiskPercent,
            UnhealthyPercent = UnhealthyPercent ?? defaults.UnhealthyPercent,
            RunwayAtRiskWeeks = RunwayAtRiskWeeks ?? defaults.RunwayAtRiskWeeks,
            RunwayUnhealthyWeeks = RunwayUnhealthyWeeks ?? defaults.RunwayUnhealthyWeeks,
            RunwayTooLongWeeks = RunwayTooLongWeeks ?? defaults.RunwayTooLongWeeks,
            NetFlowAtRisk = NetFlowAtRisk ?? defaults.NetFlowAtRisk,
            NetFlowUnhealthy = NetFlowUnhealthy ?? defaults.NetFlowUnhealthy,
            WipLoadAtRisk = WipLoadAtRisk ?? defaults.WipLoadAtRisk,
            WipLoadUnhealthy = WipLoadUnhealthy ?? defaults.WipLoadUnhealthy,
        };

        return new GetTeamBacklogHealthQuery(teamIdOrCode, thresholds, LookbackDays ?? ForecastOptions.DefaultLookbackDays);
    }
}

using Wayd.Planning.Application.PlanningIntervals.Commands;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

/// <summary>
/// Request model for mapping team sprints to Planning Interval iterations.
/// </summary>
public sealed record MapPlanningIntervalSprintsRequest
{
    /// <summary>
    /// The ID of the planning interval.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the team whose sprints are being synchronized.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// Dictionary representing the complete desired state where key is iteration ID and value is sprint ID.
    /// This is a sync/replace operation - any team sprints currently mapped but not included will be unmapped.
    /// - Non-null value: Maps the sprint to the iteration.
    /// - Null value: Explicitly unmaps any sprint from that iteration for this team.
    /// - Omitted iteration: the team's sprint mapped to it, if any, is unmapped like any other sprint not named.
    /// </summary>
    public Dictionary<Guid, Guid?> IterationSprintMappings { get; set; } = [];

    public MapPlanningIntervalSprintsCommand ToMapPlanningIntervalSprintsCommand()
    {
        return new MapPlanningIntervalSprintsCommand(Id, TeamId, IterationSprintMappings);
    }
}

public sealed class MapPlanningIntervalSprintsRequestValidator : CustomValidator<MapPlanningIntervalSprintsRequest>
{
    public MapPlanningIntervalSprintsRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Id)
            .NotEmpty()
            .WithMessage("Planning Interval ID is required.");

        RuleFor(r => r.TeamId)
            .NotEmpty()
            .WithMessage("Team ID is required.");

        RuleFor(r => r.IterationSprintMappings)
            .NotNull()
            .WithMessage("Iteration sprint mappings are required.");
    }
}


using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Domain.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Imports;

/// <summary>
/// Imports objectives onto a planning interval.
/// </summary>
public sealed class PlanningIntervalObjectiveImportDefinition(
    IPlanningDbContext planningDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportPlanningIntervalObjectiveDto>(serializer)
{
    public const string ImportKey = "planning.planning-interval-objectives";

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Planning Interval Objectives";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.PlanningIntervalObjectives;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // One transaction, so the file is bounded like every other atomic import.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportPlanningIntervalObjectiveDto>> Steps =>
    [
        new("CreateObjectives", ImportPassScope.Chunked, CreateObjectives),
    ];

    private async Task<Result> CreateObjectives(
        ImportPassContext<ImportPlanningIntervalObjectiveDto> context, CancellationToken cancellationToken)
    {
        var intervalIds = context.Rows.Select(r => r.Data.PlanningIntervalId).Distinct().ToList();

        // Tracked, not AsNoTracking: ImportObjective adds to the interval's own collection, and the
        // runner's save at the end of the chunk is what persists it.
        var intervals = await _planningDbContext.PlanningIntervals
            .Where(p => intervalIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var teamIds = context.Rows.Select(r => r.Data.TeamId).Distinct().ToList();
        var teams = await _planningDbContext.PlanningTeams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var objective = row.Data;

            if (!intervals.TryGetValue(objective.PlanningIntervalId, out var interval))
            {
                row.Failed($"No planning interval was found with id '{objective.PlanningIntervalId}'.");
                continue;
            }

            if (interval.ObjectivesLocked)
            {
                row.Failed($"Objectives are locked for planning interval '{interval.Name}'.");
                continue;
            }

            if (!teams.TryGetValue(objective.TeamId, out var team))
            {
                row.Failed($"No team was found with id '{objective.TeamId}'.");
                continue;
            }

            var created = interval.ImportObjective(
                team,
                objective.Name,
                objective.Description,
                objective.Status,
                objective.Progress,
                objective.IsStretch,
                objective.StartDate,
                objective.TargetDate,
                objective.ClosedDateUtc,
                objective.Order);

            if (created.IsFailure)
            {
                row.Failed($"The objective could not be added to the planning interval: {created.Error}");
                continue;
            }

            row.Created(created.Value.Id);
        }

        return Result.Success();
    }
}

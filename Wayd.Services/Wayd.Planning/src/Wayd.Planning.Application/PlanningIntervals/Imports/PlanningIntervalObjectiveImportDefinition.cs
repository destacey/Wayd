using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Goals.Commands;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Goals;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Extensions;
using Wayd.Planning.Domain.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Imports;

/// <summary>
/// Imports objectives onto a planning interval.
/// </summary>
/// <remarks>
/// Every other import is Atomic, matching the single save the command it replaced did. This one cannot
/// honestly claim that. An objective lives in Goals and is created by dispatching there, which Wolverine
/// runs in its own DI scope and therefore its own transaction — committed before this pass can know
/// whether the planning interval will accept it. The runner's discard only reaches what it staged, so a
/// run that says it applied nothing would still have left objectives behind.
/// <para>
/// So it is PerRow and compensates instead: a row whose objective is created but cannot be attached
/// deletes it again. That is the same two-step the old command did, kept deliberately rather than
/// inherited — closing it properly means Planning owning objective creation, which is a module boundary
/// question and not one to settle while converting an import.
/// </para>
/// </remarks>
public sealed class PlanningIntervalObjectiveImportDefinition(
    IPlanningDbContext planningDbContext,
    IDispatcher dispatcher,
    ILogger<PlanningIntervalObjectiveImportDefinition> logger,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportPlanningIntervalObjectiveDto>(serializer)
{
    public const string ImportKey = "planning.planning-interval-objectives";

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<PlanningIntervalObjectiveImportDefinition> _logger = logger;

    public override string Key => ImportKey;
    public override string DisplayName => "Planning Interval Objectives";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.PlanningIntervalObjectives;

    /// <summary>
    /// Per row, because the run cannot undo an objective another scope already committed. See the type
    /// remarks: a rejected row compensates for its own objective rather than the run discarding the lot.
    /// </summary>
    public override ImportAtomicity Atomicity => ImportAtomicity.PerRow;

    protected override IReadOnlyList<ImportPass<ImportPlanningIntervalObjectiveDto>> Steps =>
    [
        new("CreateObjectives", ImportPassScope.Chunked, CreateObjectives),
    ];

    private async Task<Result> CreateObjectives(
        ImportPassContext<ImportPlanningIntervalObjectiveDto> context, CancellationToken cancellationToken)
    {
        var intervalIds = context.Rows.Select(r => r.Data.PlanningIntervalId).Distinct().ToList();

        // Tracked, not AsNoTracking: CreateObjective adds to the interval's own collection, and the
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

            var created = await _dispatcher.Send(
                new ImportObjectiveCommand(
                    objective.Name,
                    objective.Description,
                    ObjectiveType.PlanningInterval,
                    objective.Status.ToGoalObjectiveStatus(),
                    objective.Progress,
                    objective.TeamId,
                    objective.PlanningIntervalId,
                    objective.StartDate,
                    objective.TargetDate,
                    objective.ClosedDateUtc,
                    objective.Order),
                cancellationToken);

            if (created.IsFailure)
            {
                row.Failed($"The objective could not be created: {created.Error}");
                continue;
            }

            var attached = interval.CreateObjective(team, created.Value, objective.IsStretch);
            if (attached.IsFailure)
            {
                await CompensateFor(created.Value, row.ImportId, cancellationToken);
                row.Failed($"The objective could not be added to the planning interval: {attached.Error}");
                continue;
            }

            row.Created(created.Value);
        }

        return Result.Success();
    }

    /// <summary>
    /// Deletes an objective this pass created but could not attach.
    /// </summary>
    /// <remarks>
    /// It was committed in its own scope, so nothing the runner does will remove it. A failure here leaves
    /// an orphan, which is worth a log rather than failing the row twice — the row is already rejected,
    /// and the orphan is a Goals objective belonging to no planning interval.
    /// </remarks>
    private async Task CompensateFor(Guid objectiveId, string importId, CancellationToken cancellationToken)
    {
        var deleted = await _dispatcher.Send(new DeleteObjectiveCommand(objectiveId), cancellationToken);

        if (deleted.IsFailure)
        {
            _logger.LogError(
                "Import row {ImportId} left objective {ObjectiveId} orphaned: it could not be attached, and deleting it failed with {Error}.",
                importId, objectiveId, deleted.Error);
        }
    }
}

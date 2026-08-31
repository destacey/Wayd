using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <param name="Decisions">
/// Operator choices layered over the automatic mapping — the statuses it could not decide, plus any
/// automatic choice the operator overrode.
/// </param>
public sealed record ReassignWorkflowCommand(
    Guid AssignmentId,
    Guid TargetWorkflowId,
    List<StatusRemapDecisionDto> Decisions) : ICommand<int>;

public sealed class ReassignWorkflowCommandValidator : AbstractValidator<ReassignWorkflowCommand>
{
    public ReassignWorkflowCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.TargetWorkflowId).NotEmpty();
    }
}

/// <summary>
/// Moves a scope onto another workflow and brings its records with it.
/// </summary>
/// <remarks>
/// The reason the engine exists. Everything else edits configuration; this rewrites every record of an
/// owner type, so the ordering below is deliberate.
/// <para>
/// <strong>Records move first, the assignment flips last.</strong> Both happen in one save, but should
/// the migration fail, nothing is written and the assignment still points at the workflow the records
/// are actually on. Re-running is then safe and correct, which is what <c>SwitchWorkflow</c> being a
/// no-op on already-moved records is for.
/// </para>
/// </remarks>
public sealed class ReassignWorkflowCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IStatusRecordMigrator> migrators,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReassignWorkflowCommandHandler> logger)
    : ICommandHandler<ReassignWorkflowCommand, int>
{
    private const string AppRequestName = nameof(ReassignWorkflowCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IStatusRecordMigrator> _migrators = migrators;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ReassignWorkflowCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(ReassignWorkflowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var assignment = await _dbContext.WorkflowAssignments
                .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

            if (assignment is null)
            {
                _logger.LogInformation("Workflow Assignment {AssignmentId} not found.", request.AssignmentId);
                return Result.Failure<int>("Workflow assignment not found.");
            }

            var current = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstOrDefaultAsync(w => w.Id == assignment.WorkflowId, cancellationToken);

            var target = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstOrDefaultAsync(w => w.Id == request.TargetWorkflowId, cancellationToken);

            if (current is null || target is null)
            {
                return Result.Failure<int>("Status workflow not found.");
            }

            // Already there: an interrupted migration that is re-run reaches this, and it has to be a
            // no-op rather than a failure. Checked before AutoMap, which refuses to map a workflow to
            // itself — so without this the resumability the batching is built for would not exist.
            if (assignment.WorkflowId == target.Id)
            {
                _logger.LogInformation(
                    "{OwnerType} is already assigned to workflow {WorkflowId}; nothing to migrate.",
                    assignment.OwnerType, target.Id);

                return Result.Success(0);
            }

            // Recomputed rather than carried across from the preview: a remap is a value, not a
            // process, and AutoMap is pure over two loaded workflows. The operator's decisions are
            // then layered on top, which also lets Resolve override an automatic choice.
            var remap = StatusRemap.AutoMap(current, target);
            if (remap.IsFailure)
            {
                return Result.Failure<int>(remap.Error);
            }

            foreach (var decision in request.Decisions)
            {
                var status = target.Statuses.FirstOrDefault(s => s.Id == decision.ToStatusId);
                if (status is null)
                {
                    return Result.Failure<int>("A chosen status does not belong to the workflow being moved to.");
                }

                var resolved = remap.Value.Resolve(decision.FromStatusId, status);
                if (resolved.IsFailure)
                {
                    return Result.Failure<int>(resolved.Error);
                }
            }

            if (!remap.Value.IsComplete)
            {
                var unresolved = string.Join(", ", remap.Value.Unresolved.Select(s => s.Name));

                return Result.Failure<int>(
                    $"These statuses have nowhere to go: {unresolved}. Map every status before reassigning.");
            }

            var migrator = _migrators.FirstOrDefault(m =>
                string.Equals(m.OwnerType, assignment.OwnerType, StringComparison.OrdinalIgnoreCase));

            if (migrator is null)
            {
                // Refused rather than skipped: a missing migrator would leave every record on the old
                // workflow while the assignment claimed otherwise, which is worse than not moving.
                _logger.LogError("No record migrator is registered for owner type {OwnerType}.", assignment.OwnerType);
                return Result.Failure<int>($"No records can be migrated for {assignment.OwnerType}.");
            }

            var actor = EventActor.User(_currentUser.GetUserId());
            var now = _dateTimeProvider.Now;

            var migrated = await migrator.Migrate(remap.Value, assignment.ScopeId, actor, now, cancellationToken);
            if (migrated.IsFailure)
            {
                return Result.Failure<int>(migrated.Error);
            }

            // Last, so a failure above leaves the assignment pointing at the workflow the records are
            // still on.
            var reassigned = assignment.ReassignTo(target, remap.Value, actor, now);
            if (reassigned.IsFailure)
            {
                assignment.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to reassign {OwnerType}. Error message: {Error}", assignment.OwnerType, reassigned.Error);
                return Result.Failure<int>(reassigned.Error);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "{OwnerType} reassigned to workflow {WorkflowId}; {Count} record(s) migrated.",
                assignment.OwnerType, target.Id, migrated.Value);

            return Result.Success(migrated.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<int>($"Error handling {AppRequestName} command.");
        }
    }
}

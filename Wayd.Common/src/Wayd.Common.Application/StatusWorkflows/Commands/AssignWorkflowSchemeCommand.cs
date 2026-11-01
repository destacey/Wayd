using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <param name="Decisions">
/// Operator choices layered over the automatic mappings, for every record type whose workflow differs
/// between the two schemes. Each decision's two statuses name the pair of workflows it applies to.
/// </param>
public sealed record AssignWorkflowSchemeCommand(
    Guid AssignmentId,
    Guid SchemeId,
    List<StatusRemapDecisionDto> Decisions) : ICommand<int>;

public sealed class AssignWorkflowSchemeCommandValidator : AbstractValidator<AssignWorkflowSchemeCommand>
{
    public AssignWorkflowSchemeCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.SchemeId).NotEmpty();
    }
}

/// <summary>
/// Points a scope at another scheme of its owner type, moving each record type whose workflow differs
/// between the two — and only those.
/// </summary>
/// <remarks>
/// For owner types with record types; one without them picks a workflow (<see cref="ReassignWorkflowCommand"/>).
/// The assignment flips before records move, so a reassignment it refuses moves nothing; both land in one save.
/// </remarks>
public sealed class AssignWorkflowSchemeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IRecordTypeStatusMigrator> migrators,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<AssignWorkflowSchemeCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<AssignWorkflowSchemeCommand, int>
{
    private const string AppRequestName = nameof(AssignWorkflowSchemeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IRecordTypeStatusMigrator> _migrators = migrators;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<AssignWorkflowSchemeCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<int>> Handle(AssignWorkflowSchemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var assignment = await _dbContext.WorkflowAssignments
                .Include(a => a.Scheme)
                    .ThenInclude(s => s!.Entries)
                .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);
            if (assignment is null || !await _ownerAvailability.IsAvailable(assignment.OwnerType, cancellationToken))
            {
                return Result.Failure<int>("Workflow assignment not found.");
            }

            if (WorkflowOwners.Resolve(assignment.OwnerType) is not { IsSuccess: true } descriptor || !descriptor.Value.HasRecordTypes)
            {
                return Result.Failure<int>("These records do not have record types. Pick a workflow for the scope instead.");
            }

            var target = await _dbContext.StatusWorkflowSchemes
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == request.SchemeId && !s.IsImplicit, cancellationToken);
            if (target is null)
            {
                return Result.Failure<int>("Workflow scheme not found.");
            }

            if (target.Id == assignment.SchemeId)
            {
                return Result.Success(0);
            }

            var moves = SchemeRecordMoves.ForAssignment(assignment.Scheme!, target);

            var defaultWorkflow = await _dbContext.StatusWorkflows.FirstOrDefaultAsync(w => w.Id == target.DefaultWorkflowId, cancellationToken);
            if (defaultWorkflow is null)
            {
                return Result.Failure<int>("Status workflow not found.");
            }

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
            var now = _dateTimeProvider.Now;

            var plan = await SchemeRecordMoves.Plan(
                _dbContext, _migrators, assignment.OwnerType, moves, [assignment.ScopeId], request.Decisions, cancellationToken);
            if (plan.IsFailure)
            {
                return Result.Failure<int>(plan.Error);
            }

            var reassigned = assignment.ReassignTo(target, defaultWorkflow, plan.Value.Remaps, actor, now);
            if (reassigned.IsFailure)
            {
                assignment.ClearDomainEvents();
                return Result.Failure<int>(reassigned.Error);
            }

            var moved = await SchemeRecordMoves.Migrate(plan.Value, actor, now, cancellationToken);
            if (moved.IsFailure)
            {
                assignment.ClearDomainEvents();
                return moved;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return moved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<int>($"Error handling {AppRequestName} command.");
        }
    }
}

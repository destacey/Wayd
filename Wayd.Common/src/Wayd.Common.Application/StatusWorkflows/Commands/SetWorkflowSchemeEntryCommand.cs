using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <param name="WorkflowId">
/// The workflow records of the type follow from now on, or <c>null</c> to remove the type's entry so it
/// follows the scheme's default.
/// </param>
/// <param name="Decisions">
/// Operator choices layered over the automatic mapping from the type's current workflow. Needed only when the
/// scheme is assigned, since only then do records move.
/// </param>
public sealed record SetWorkflowSchemeEntryCommand(
    Guid SchemeId,
    Guid RecordTypeId,
    Guid? WorkflowId,
    List<StatusRemapDecisionDto> Decisions) : ICommand<int>;

public sealed class SetWorkflowSchemeEntryCommandValidator : AbstractValidator<SetWorkflowSchemeEntryCommand>
{
    public SetWorkflowSchemeEntryCommandValidator()
    {
        RuleFor(x => x.SchemeId).NotEmpty();
        RuleFor(x => x.RecordTypeId).NotEmpty();
        RuleFor(x => x.WorkflowId).NotEqual(Guid.Empty);
    }
}

/// <summary>
/// Changes which workflow one record type follows under a scheme, moving that type's records — and only
/// theirs — in every scope on the scheme.
/// </summary>
/// <remarks>
/// The entry changes before records move, so a change the scheme refuses moves nothing; both land in one save.
/// </remarks>
public sealed class SetWorkflowSchemeEntryCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IRecordTypeStatusMigrator> migrators,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetWorkflowSchemeEntryCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<SetWorkflowSchemeEntryCommand, int>
{
    private const string AppRequestName = nameof(SetWorkflowSchemeEntryCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IRecordTypeStatusMigrator> _migrators = migrators;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SetWorkflowSchemeEntryCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<int>> Handle(SetWorkflowSchemeEntryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scheme = await _dbContext.StatusWorkflowSchemes
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == request.SchemeId, cancellationToken);
            if (scheme is null || scheme.IsImplicit || !await _ownerAvailability.IsAvailable(scheme.OwnerType, cancellationToken))
            {
                return Result.Failure<int>("Workflow scheme not found.");
            }

            var recordType = await _dbContext.RecordTypes.FirstOrDefaultAsync(r => r.Id == request.RecordTypeId, cancellationToken);
            if (recordType is null)
            {
                return Result.Failure<int>("Record type not found.");
            }

            var workflow = request.WorkflowId is { } workflowId
                ? await _dbContext.StatusWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
                : null;
            if (request.WorkflowId is not null && workflow is null)
            {
                return Result.Failure<int>("Status workflow not found.");
            }

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
            var now = _dateTimeProvider.Now;

            var scopeIds = await _dbContext.AssignedScopes(scheme.Id, cancellationToken);
            var plan = await SchemeRecordMoves.Plan(
                _dbContext, _migrators, scheme.OwnerType,
                [SchemeRecordMoves.ForEntry(scheme, recordType.Id, request.WorkflowId)],
                scopeIds, request.Decisions, cancellationToken);
            if (plan.IsFailure)
            {
                return Result.Failure<int>(plan.Error);
            }

            var remap = plan.Value.Remaps.FirstOrDefault();
            var changed = workflow is not null
                ? scheme.SetEntry(recordType, workflow, remap, actor, now)
                : scheme.RemoveEntry(recordType.Id, remap, actor, now);
            if (changed.IsFailure)
            {
                scheme.ClearDomainEvents();
                scheme.ClearPostPersistenceActions();
                _logger.LogInformation("Unable to change Workflow Scheme {SchemeId}. Error message: {Error}", scheme.Id, changed.Error);
                return Result.Failure<int>(changed.Error);
            }

            var moved = await SchemeRecordMoves.Migrate(plan.Value, actor, now, cancellationToken);
            if (moved.IsFailure)
            {
                scheme.ClearDomainEvents();
                scheme.ClearPostPersistenceActions();
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

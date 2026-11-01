using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <param name="Decisions">
/// Operator choices layered over the automatic mapping from the current default. Needed only when the scheme
/// is assigned, since only then do records move.
/// </param>
public sealed record ChangeWorkflowSchemeDefaultCommand(
    Guid SchemeId,
    Guid WorkflowId,
    List<StatusRemapDecisionDto> Decisions) : ICommand<int>;

public sealed class ChangeWorkflowSchemeDefaultCommandValidator : AbstractValidator<ChangeWorkflowSchemeDefaultCommand>
{
    public ChangeWorkflowSchemeDefaultCommandValidator()
    {
        RuleFor(x => x.SchemeId).NotEmpty();
        RuleFor(x => x.WorkflowId).NotEmpty();
    }
}

/// <summary>
/// Changes the workflow followed by every record whose type has no entry, in every scope on the scheme.
/// </summary>
public sealed class ChangeWorkflowSchemeDefaultCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IEnumerable<IRecordTypeStatusMigrator> migrators,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<ChangeWorkflowSchemeDefaultCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<ChangeWorkflowSchemeDefaultCommand, int>
{
    private const string AppRequestName = nameof(ChangeWorkflowSchemeDefaultCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IEnumerable<IRecordTypeStatusMigrator> _migrators = migrators;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ChangeWorkflowSchemeDefaultCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<int>> Handle(ChangeWorkflowSchemeDefaultCommand request, CancellationToken cancellationToken)
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

            var workflow = await _dbContext.StatusWorkflows.FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);
            if (workflow is null)
            {
                return Result.Failure<int>("Status workflow not found.");
            }

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
            var now = _dateTimeProvider.Now;

            var scopeIds = await _dbContext.AssignedScopes(scheme.Id, cancellationToken);
            var moves = await SchemeRecordMoves.Apply(
                _dbContext, _migrators, scheme.OwnerType,
                [SchemeRecordMoves.ForDefault(scheme, workflow.Id)],
                scopeIds, request.Decisions, actor, now, cancellationToken);
            if (moves.IsFailure)
            {
                return Result.Failure<int>(moves.Error);
            }

            if (moves.Value.Remaps.FirstOrDefault() is not { } remap)
            {
                return Result.Success(0);
            }

            var changed = scheme.ChangeDefault(workflow, remap, actor, now);
            if (changed.IsFailure)
            {
                scheme.ClearDomainEvents();
                scheme.ClearPostPersistenceActions();
                return Result.Failure<int>(changed.Error);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(moves.Value.Moved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<int>($"Error handling {AppRequestName} command.");
        }
    }
}

using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Points one scope at the managed scheme its source uses — an Azure DevOps project at its process —
/// assigning it the first time.
/// </summary>
/// <remarks>
/// For the sync; no endpoint dispatches it. Records in the scope are not moved: they take their status from
/// the source, so each lands in its type's workflow on its own next sync.
/// </remarks>
/// <param name="ScopeId">The scope. Never the organization: a sync assigns only the containers it keeps.</param>
/// <param name="SchemeExternalKey">The source key of a managed scheme kept by <see cref="SyncManagedWorkflowSchemeCommand"/>.</param>
/// <returns>The assignment's id.</returns>
public sealed record SyncManagedWorkflowAssignmentCommand(string OwnerType, Guid ScopeId, string SchemeExternalKey) : ICommand<Guid>;

public sealed class SyncManagedWorkflowAssignmentCommandValidator : AbstractValidator<SyncManagedWorkflowAssignmentCommand>
{
    public SyncManagedWorkflowAssignmentCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ScopeId).NotEmpty();
        RuleFor(x => x.SchemeExternalKey).NotEmpty().MaximumLength(256);
    }
}

public sealed class SyncManagedWorkflowAssignmentCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<SyncManagedWorkflowAssignmentCommandHandler> logger)
    : ICommandHandler<SyncManagedWorkflowAssignmentCommand, Guid>
{
    private const string AppRequestName = nameof(SyncManagedWorkflowAssignmentCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SyncManagedWorkflowAssignmentCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(SyncManagedWorkflowAssignmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Registration, not availability: a feature flag hides an owner type from administration, but
            // its source still changes, and the sync keeps up with it.
            if (WorkflowOwners.Resolve(request.OwnerType).IsFailure)
            {
                return Result.Failure<Guid>($"'{request.OwnerType}' is not a registered workflow owner type.");
            }

            var schemeKey = request.SchemeExternalKey.Trim();
            var scheme = await _dbContext.StatusWorkflowSchemes
                .FirstOrDefaultAsync(s => s.OwnerType == request.OwnerType && s.ExternalKey == schemeKey, cancellationToken);
            if (scheme is null)
            {
                return Result.Failure<Guid>("Workflow scheme not found.");
            }

            var defaultWorkflow = await _dbContext.StatusWorkflows
                .FirstOrDefaultAsync(w => w.Id == scheme.DefaultWorkflowId, cancellationToken);
            if (defaultWorkflow is null)
            {
                return Result.Failure<Guid>("Status workflow not found.");
            }

            var actor = EventActor.Sync(null);
            var now = _dateTimeProvider.Now;

            var assignment = await _dbContext.WorkflowAssignments
                .Include(a => a.Scheme)
                .FirstOrDefaultAsync(a => a.OwnerType == request.OwnerType && a.ScopeId == request.ScopeId, cancellationToken);

            if (assignment is null)
            {
                var created = WorkflowAssignment.Create(request.OwnerType, request.ScopeId, scheme, defaultWorkflow, actor, now);
                if (created.IsFailure)
                {
                    _logger.LogWarning("Unable to assign managed scheme {SchemeId} to scope {ScopeId}. Error message: {Error}",
                        scheme.Id, request.ScopeId, created.Error);
                    return Result.Failure<Guid>(created.Error);
                }

                assignment = created.Value;
                _dbContext.WorkflowAssignments.Add(assignment);
            }
            else
            {
                var synced = assignment.SyncFromSource(scheme, defaultWorkflow, actor, now);
                if (synced.IsFailure)
                {
                    assignment.ClearDomainEvents();
                    assignment.ClearPostPersistenceActions();

                    _logger.LogWarning("Unable to reassign scope {ScopeId} to managed scheme {SchemeId}. Error message: {Error}",
                        request.ScopeId, scheme.Id, synced.Error);
                    return Result.Failure<Guid>(synced.Error);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(assignment.Id);
        }
        catch (DbUpdateException ex) when (ManagedSyncCollision.IsCollision(ex))
        {
            _logger.LogWarning(ex, "{CommandName}: the save failed at the database.", AppRequestName);
            return Result.Failure<Guid>(ManagedSyncCollision.Message("workflow assignment", $"{request.SchemeExternalKey} for scope {request.ScopeId}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}

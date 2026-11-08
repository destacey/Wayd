using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Brings the managed workflow mirroring one source workflow into line with it, creating it the first time.
/// <see cref="Statuses"/> is every status the source has now, in its order; a status left out is retired.
/// </summary>
/// <remarks>
/// The sync's only way to change a managed workflow's structure; no endpoint dispatches it. The workflow
/// is found by <see cref="OwnerType"/> and <see cref="ExternalKey"/>, which the sync namespaces by its source
/// object (a work process) so two sources cannot collide.
/// </remarks>
/// <returns>The workflow's id.</returns>
public sealed record SyncManagedWorkflowCommand(
    string OwnerType,
    string ExternalKey,
    string Name,
    IReadOnlyList<SourceStatus> Statuses) : ICommand<Guid>;

public sealed class SyncManagedWorkflowCommandValidator : AbstractValidator<SyncManagedWorkflowCommand>
{
    public SyncManagedWorkflowCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExternalKey).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Statuses).NotEmpty();
        RuleForEach(x => x.Statuses).ChildRules(status =>
        {
            status.RuleFor(s => s.ExternalKey).NotEmpty().MaximumLength(256);
            status.RuleFor(s => s.Name).NotEmpty().MaximumLength(64);
            status.RuleFor(s => s.Category).IsInEnum();
            status.RuleFor(s => s.Description).MaximumLength(512);
        });
    }
}

public sealed class SyncManagedWorkflowCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<SyncManagedWorkflowCommandHandler> logger)
    : ICommandHandler<SyncManagedWorkflowCommand, Guid>
{
    private const string AppRequestName = nameof(SyncManagedWorkflowCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SyncManagedWorkflowCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(SyncManagedWorkflowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Registration, not availability: a feature flag hides an owner type from administration, but
            // its source still changes, and the sync keeps up with it.
            if (WorkflowOwners.Resolve(request.OwnerType).IsFailure)
            {
                return Result.Failure<Guid>($"'{request.OwnerType}' is not a registered workflow owner type.");
            }

            var externalKey = request.ExternalKey.Trim();
            var actor = EventActor.Sync(null);
            var now = _dateTimeProvider.Now;

            // The whole status collection is loaded, retired statuses included: a status the source brings
            // back is matched by its key and reinstated rather than added again.
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstOrDefaultAsync(w => w.OwnerType == request.OwnerType && w.ExternalKey == externalKey, cancellationToken);

            if (workflow is null)
            {
                var created = StatusWorkflow.CreateManaged(request.Name, request.OwnerType, externalKey, request.Statuses, actor, now);
                if (created.IsFailure)
                {
                    _logger.LogWarning("Unable to create managed workflow {ExternalKey}. Error message: {Error}", externalKey, created.Error);
                    return Result.Failure<Guid>(created.Error);
                }

                workflow = created.Value;
                _dbContext.StatusWorkflows.Add(workflow);
            }
            else
            {
                var synced = workflow.SyncFromSource(request.Name, request.Statuses, actor, now);
                if (synced.IsFailure)
                {
                    workflow.ClearDomainEvents();
                    workflow.ClearPostPersistenceActions();

                    _logger.LogWarning(
                        "Unable to sync managed workflow {WorkflowId}. Error message: {Error}", workflow.Id, synced.Error);
                    return Result.Failure<Guid>(synced.Error);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(workflow.Id);
        }
        catch (DbUpdateException ex) when (ManagedSyncCollision.IsCollision(ex))
        {
            _logger.LogWarning(ex, "{CommandName}: the save failed at the database for {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>(ManagedSyncCollision.Message("workflow", request.ExternalKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}

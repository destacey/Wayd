using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Brings the managed scheme mirroring one source object (an Azure DevOps process) into line with it,
/// creating it the first time. <see cref="Entries"/> names the workflow each of the source's record types
/// follows.
/// </summary>
/// <remarks>
/// For the sync; no endpoint dispatches it. The scheme is found by <see cref="OwnerType"/> and
/// <see cref="ExternalKey"/>. Every workflow named must already exist, so the sync runs
/// <see cref="SyncManagedWorkflowCommand"/> and <see cref="SyncManagedRecordTypeCommand"/> first.
/// </remarks>
/// <param name="OwnerType">The registered owner type whose records the scheme governs.</param>
/// <param name="ExternalKey">The source object's stable identifier, namespaced by the sync.</param>
/// <param name="Name">What the source calls the object, which the scheme takes as its name.</param>
/// <param name="DefaultWorkflowId">The workflow a record follows when its type has no entry.</param>
/// <param name="Entries">The workflow each of the source's record types follows.</param>
/// <returns>The scheme's id.</returns>
public sealed record SyncManagedWorkflowSchemeCommand(
    string OwnerType,
    string ExternalKey,
    string Name,
    Guid DefaultWorkflowId,
    IReadOnlyList<ManagedSchemeEntry> Entries) : ICommand<Guid>;

/// <summary>One record type of a managed scheme and the workflow it follows.</summary>
/// <param name="RecordTypeId">The record type, from <see cref="SyncManagedRecordTypeCommand"/>.</param>
/// <param name="WorkflowId">Its workflow, from <see cref="SyncManagedWorkflowCommand"/>.</param>
public sealed record ManagedSchemeEntry(Guid RecordTypeId, Guid WorkflowId);

public sealed class SyncManagedWorkflowSchemeCommandValidator : AbstractValidator<SyncManagedWorkflowSchemeCommand>
{
    public SyncManagedWorkflowSchemeCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExternalKey).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DefaultWorkflowId).NotEmpty();
        RuleFor(x => x.Entries).NotNull();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.RecordTypeId).NotEmpty();
            entry.RuleFor(e => e.WorkflowId).NotEmpty();
        });
    }
}

public sealed class SyncManagedWorkflowSchemeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<SyncManagedWorkflowSchemeCommandHandler> logger)
    : ICommandHandler<SyncManagedWorkflowSchemeCommand, Guid>
{
    private const string AppRequestName = nameof(SyncManagedWorkflowSchemeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SyncManagedWorkflowSchemeCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(SyncManagedWorkflowSchemeCommand request, CancellationToken cancellationToken)
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

            var workflowIds = request.Entries.Select(e => e.WorkflowId).Append(request.DefaultWorkflowId).Distinct().ToList();
            var workflows = await _dbContext.StatusWorkflows
                .Where(w => workflowIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, cancellationToken);

            var recordTypeIds = request.Entries.Select(e => e.RecordTypeId).Distinct().ToList();
            var recordTypes = await _dbContext.RecordTypes
                .Where(r => recordTypeIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);

            if (!workflows.TryGetValue(request.DefaultWorkflowId, out var defaultWorkflow)
                || request.Entries.Any(e => !workflows.ContainsKey(e.WorkflowId)))
            {
                return Result.Failure<Guid>("Status workflow not found.");
            }

            if (request.Entries.Any(e => !recordTypes.ContainsKey(e.RecordTypeId)))
            {
                return Result.Failure<Guid>("Record type not found.");
            }

            var entries = request.Entries
                .Select(e => (recordTypes[e.RecordTypeId], workflows[e.WorkflowId]))
                .ToList();

            var scheme = await _dbContext.StatusWorkflowSchemes
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.OwnerType == request.OwnerType && s.ExternalKey == externalKey, cancellationToken);

            if (scheme is null)
            {
                var created = StatusWorkflowScheme.CreateManaged(request.OwnerType, request.Name, externalKey, defaultWorkflow, actor, now, entries);
                if (created.IsFailure)
                {
                    _logger.LogWarning("Unable to create managed workflow scheme {ExternalKey}. Error message: {Error}", externalKey, created.Error);
                    return Result.Failure<Guid>(created.Error);
                }

                scheme = created.Value;
                _dbContext.StatusWorkflowSchemes.Add(scheme);
            }
            else
            {
                var synced = scheme.SyncFromSource(request.Name, defaultWorkflow, entries, actor, now);
                if (synced.IsFailure)
                {
                    scheme.ClearDomainEvents();
                    scheme.ClearPostPersistenceActions();

                    _logger.LogWarning("Unable to sync managed workflow scheme {SchemeId}. Error message: {Error}", scheme.Id, synced.Error);
                    return Result.Failure<Guid>(synced.Error);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(scheme.Id);
        }
        catch (DbUpdateException ex) when (ManagedSyncCollision.IsCollision(ex))
        {
            _logger.LogWarning(ex, "{CommandName}: the save failed at the database for {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>(ManagedSyncCollision.Message("workflow scheme", request.ExternalKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}

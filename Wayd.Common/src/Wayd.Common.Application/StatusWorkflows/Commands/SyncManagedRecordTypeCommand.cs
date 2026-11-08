using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Brings the managed record type mirroring one source type into line with it: creates it the first time,
/// takes the source's name, and reinstates it if it had been retired.
/// </summary>
/// <remarks>
/// For the sync; no endpoint dispatches it. Record types are organization-wide per owner type and several
/// sources may supply them, so each is synced and retired on its own rather than as a set.
/// </remarks>
/// <returns>The record type's id.</returns>
public sealed record SyncManagedRecordTypeCommand(string OwnerType, string ExternalKey, string Name) : ICommand<Guid>;

public sealed class SyncManagedRecordTypeCommandValidator : AbstractValidator<SyncManagedRecordTypeCommand>
{
    public SyncManagedRecordTypeCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExternalKey).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

public sealed class SyncManagedRecordTypeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<SyncManagedRecordTypeCommandHandler> logger)
    : ICommandHandler<SyncManagedRecordTypeCommand, Guid>
{
    private const string AppRequestName = nameof(SyncManagedRecordTypeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SyncManagedRecordTypeCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(SyncManagedRecordTypeCommand request, CancellationToken cancellationToken)
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

            var recordType = await _dbContext.RecordTypes
                .FirstOrDefaultAsync(r => r.OwnerType == request.OwnerType && r.ExternalKey == externalKey, cancellationToken);

            var result = recordType is null
                ? RecordType.CreateManaged(request.OwnerType, request.Name, externalKey, actor, now)
                : recordType.SyncFromSource(request.Name, actor, now).Map(() => recordType);

            if (result.IsFailure)
            {
                recordType?.ClearDomainEvents();

                _logger.LogWarning("Unable to sync managed record type {ExternalKey}. Error message: {Error}", externalKey, result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            var synced = result.Value;
            if (await RecordTypeNames.IsTaken(_dbContext, synced.OwnerType, synced.Name, exceptId: synced.Id, cancellationToken))
            {
                synced.ClearDomainEvents();
                return Result.Failure<Guid>($"A record type named '{synced.Name}' already exists.");
            }

            if (recordType is null)
            {
                _dbContext.RecordTypes.Add(synced);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(synced.Id);
        }
        catch (DbUpdateException ex) when (ManagedSyncCollision.IsCollision(ex))
        {
            _logger.LogWarning(ex, "{CommandName}: the save failed at the database for {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>(ManagedSyncCollision.Message("record type", request.ExternalKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}

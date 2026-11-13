using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Retires the managed record type mirroring a source type the source has dropped. Records and scheme
/// entries keep it; no record can be given it until <see cref="SyncManagedRecordTypeCommand"/> brings it back.
/// </summary>
/// <remarks>For the sync; no endpoint dispatches it.</remarks>
public sealed record RetireManagedRecordTypeCommand(string OwnerType, string ExternalKey) : ICommand;

public sealed class RetireManagedRecordTypeCommandValidator : AbstractValidator<RetireManagedRecordTypeCommand>
{
    public RetireManagedRecordTypeCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExternalKey).NotEmpty().MaximumLength(256);
    }
}

public sealed class RetireManagedRecordTypeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<RetireManagedRecordTypeCommandHandler> logger)
    : ICommandHandler<RetireManagedRecordTypeCommand>
{
    private const string AppRequestName = nameof(RetireManagedRecordTypeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RetireManagedRecordTypeCommandHandler> _logger = logger;

    public async Task<Result> Handle(RetireManagedRecordTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var externalKey = request.ExternalKey.Trim();

            var recordType = await _dbContext.RecordTypes
                .FirstOrDefaultAsync(r => r.OwnerType == request.OwnerType && r.ExternalKey == externalKey, cancellationToken);

            if (recordType is null)
            {
                _logger.LogInformation("Managed record type {ExternalKey} not found.", externalKey);
                return Result.Failure("Record type not found.");
            }

            var result = recordType.RetireFromSource(EventActor.Sync(null), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                recordType.ClearDomainEvents();
                return result;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

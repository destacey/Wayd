using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>Renames an owned record type.</summary>
public sealed record RenameRecordTypeCommand(Guid Id, string Name) : ICommand;

public sealed class RenameRecordTypeCommandValidator : AbstractValidator<RenameRecordTypeCommand>
{
    public RenameRecordTypeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

public sealed class RenameRecordTypeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<RenameRecordTypeCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<RenameRecordTypeCommand>
{
    private const string AppRequestName = nameof(RenameRecordTypeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RenameRecordTypeCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result> Handle(RenameRecordTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var recordType = await _dbContext.RecordTypes.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (recordType is null || !await _ownerAvailability.IsAvailable(recordType.OwnerType, cancellationToken))
            {
                return Result.Failure("Record type not found.");
            }

            if (await RecordTypeNames.IsTaken(_dbContext, recordType.OwnerType, request.Name.Trim(), recordType.Id, cancellationToken))
            {
                return Result.Failure($"A record type named '{request.Name.Trim()}' already exists.");
            }

            var result = recordType.Rename(request.Name, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogInformation("Unable to rename Record Type {RecordTypeId}. Error message: {Error}", request.Id, result.Error);
                return result;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

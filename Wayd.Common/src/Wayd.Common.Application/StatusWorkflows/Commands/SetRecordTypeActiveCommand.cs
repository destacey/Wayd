using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>Activates or deactivates a record type. Records that have it keep it either way.</summary>
public sealed record SetRecordTypeActiveCommand(Guid Id, bool IsActive) : ICommand;

public sealed class SetRecordTypeActiveCommandValidator : AbstractValidator<SetRecordTypeActiveCommand>
{
    public SetRecordTypeActiveCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class SetRecordTypeActiveCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetRecordTypeActiveCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<SetRecordTypeActiveCommand>
{
    private const string AppRequestName = nameof(SetRecordTypeActiveCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SetRecordTypeActiveCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result> Handle(SetRecordTypeActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var recordType = await _dbContext.RecordTypes.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (recordType is null || !await _ownerAvailability.IsAvailable(recordType.OwnerType, cancellationToken))
            {
                return Result.Failure("Record type not found.");
            }

            var actor = EventActor.User(_currentUser.GetUserId());
            var result = request.IsActive
                ? recordType.Activate(actor, _dateTimeProvider.Now)
                : recordType.Deactivate(actor, _dateTimeProvider.Now);
            if (result.IsFailure)
            {
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

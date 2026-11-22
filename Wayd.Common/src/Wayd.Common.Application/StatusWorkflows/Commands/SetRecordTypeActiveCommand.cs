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
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<SetRecordTypeActiveCommand>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result> Handle(SetRecordTypeActiveCommand request, CancellationToken cancellationToken)
    {
        var recordType = await _dbContext.RecordTypes.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (recordType is null || !await _ownerAvailability.IsAvailable(recordType.OwnerType, cancellationToken))
        {
            return Result.Failure("Record type not found.");
        }

        if (request.IsActive && !recordType.IsActive
            && await RecordTypeNames.IsTaken(_dbContext, recordType.OwnerType, recordType.Name, recordType.Id, cancellationToken))
        {
            return Result.Failure($"Another active record type is named '{recordType.Name}'. Rename one of them first.");
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
}

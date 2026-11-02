using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>Defines a record type for an owner type that has them.</summary>
public sealed record CreateRecordTypeCommand(string OwnerType, string Name) : ICommand<Guid>;

public sealed class CreateRecordTypeCommandValidator : AbstractValidator<CreateRecordTypeCommand>
{
    public CreateRecordTypeCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

public sealed class CreateRecordTypeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateRecordTypeCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<CreateRecordTypeCommand, Guid>
{
    private const string AppRequestName = nameof(CreateRecordTypeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateRecordTypeCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<Guid>> Handle(CreateRecordTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _ownerAvailability.IsAvailable(request.OwnerType, cancellationToken))
            {
                return Result.Failure<Guid>($"'{request.OwnerType}' is not a registered workflow owner type.");
            }

            var created = RecordType.Create(request.OwnerType, request.Name, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (created.IsFailure)
            {
                _logger.LogInformation("Unable to create a record type. Error message: {Error}", created.Error);
                return Result.Failure<Guid>(created.Error);
            }

            var recordType = created.Value;
            if (await RecordTypeNames.IsTaken(_dbContext, recordType.OwnerType, recordType.Name, exceptId: null, cancellationToken))
            {
                return Result.Failure<Guid>($"A record type named '{recordType.Name}' already exists.");
            }

            _dbContext.RecordTypes.Add(recordType);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(recordType.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}

/// <summary>Whether a record type name is already in use within its owner type.</summary>
internal static class RecordTypeNames
{
    public static Task<bool> IsTaken(
        IStatusWorkflowDbContext dbContext, string ownerType, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.RecordTypes.AnyAsync(
            r => r.OwnerType == ownerType && r.Name == name && r.Id != exceptId,
            cancellationToken);
}

using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Creates an owned workflow scheme for an owner type with record types, sending every type to the
/// default workflow until entries say otherwise.
/// </summary>
public sealed record CreateWorkflowSchemeCommand(string OwnerType, string Name, string? Description, Guid DefaultWorkflowId) : ICommand<Guid>;

public sealed class CreateWorkflowSchemeCommandValidator : AbstractValidator<CreateWorkflowSchemeCommand>
{
    public CreateWorkflowSchemeCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.DefaultWorkflowId).NotEmpty();
    }
}

public sealed class CreateWorkflowSchemeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateWorkflowSchemeCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<CreateWorkflowSchemeCommand, Guid>
{
    private const string AppRequestName = nameof(CreateWorkflowSchemeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateWorkflowSchemeCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<Guid>> Handle(CreateWorkflowSchemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _ownerAvailability.IsAvailable(request.OwnerType, cancellationToken))
            {
                return Result.Failure<Guid>($"'{request.OwnerType}' is not a registered workflow owner type.");
            }

            var workflow = await _dbContext.StatusWorkflows
                .FirstOrDefaultAsync(w => w.Id == request.DefaultWorkflowId, cancellationToken);
            if (workflow is null)
            {
                return Result.Failure<Guid>("Status workflow not found.");
            }

            var created = StatusWorkflowScheme.Create(
                request.OwnerType, request.Name, request.Description, workflow, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (created.IsFailure)
            {
                _logger.LogInformation("Unable to create a workflow scheme. Error message: {Error}", created.Error);
                return Result.Failure<Guid>(created.Error);
            }

            _dbContext.StatusWorkflowSchemes.Add(created.Value);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(created.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}

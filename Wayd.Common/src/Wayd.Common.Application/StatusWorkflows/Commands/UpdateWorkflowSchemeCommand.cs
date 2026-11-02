using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>Edits a workflow scheme's name and description.</summary>
public sealed record UpdateWorkflowSchemeCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateWorkflowSchemeCommandValidator : AbstractValidator<UpdateWorkflowSchemeCommand>
{
    public UpdateWorkflowSchemeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
    }
}

public sealed class UpdateWorkflowSchemeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdateWorkflowSchemeCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<UpdateWorkflowSchemeCommand>
{
    private const string AppRequestName = nameof(UpdateWorkflowSchemeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateWorkflowSchemeCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result> Handle(UpdateWorkflowSchemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scheme = await _dbContext.StatusWorkflowSchemes.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (scheme is null || scheme.IsImplicit || !await _ownerAvailability.IsAvailable(scheme.OwnerType, cancellationToken))
            {
                return Result.Failure("Workflow scheme not found.");
            }

            var result = scheme.UpdateDetails(request.Name, request.Description, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
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

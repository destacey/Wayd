using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>Deletes a workflow scheme no scope points at.</summary>
public sealed record DeleteWorkflowSchemeCommand(Guid Id) : ICommand;

public sealed class DeleteWorkflowSchemeCommandValidator : AbstractValidator<DeleteWorkflowSchemeCommand>
{
    public DeleteWorkflowSchemeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class DeleteWorkflowSchemeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<DeleteWorkflowSchemeCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<DeleteWorkflowSchemeCommand>
{
    private const string AppRequestName = nameof(DeleteWorkflowSchemeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeleteWorkflowSchemeCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result> Handle(DeleteWorkflowSchemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scheme = await _dbContext.StatusWorkflowSchemes
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (scheme is null || scheme.IsImplicit || !await _ownerAvailability.IsAvailable(scheme.OwnerType, cancellationToken))
            {
                return Result.Failure("Workflow scheme not found.");
            }

            var isAssigned = await _dbContext.WorkflowAssignments.AnyAsync(a => a.SchemeId == scheme.Id, cancellationToken);

            var result = scheme.Delete(isAssigned, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                return result;
            }

            _dbContext.StatusWorkflowSchemes.Remove(scheme);
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

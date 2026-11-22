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
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<DeleteWorkflowSchemeCommand>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result> Handle(DeleteWorkflowSchemeCommand request, CancellationToken cancellationToken)
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
}

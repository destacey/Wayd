using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.DemandManagement.Domain;
using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Deletes a desk with no requests on it. The Administer permission is checked by the endpoint.
/// </summary>
/// <remarks>
/// Every request is on exactly one desk, with no unassigned state, so a desk with requests on it cannot go;
/// closing it, and making it one requesters cannot choose, is the way to retire it. A desk changed between
/// reading and deleting it is refused like a stale update, by the revision's concurrency check.
/// </remarks>
/// <param name="Id">The desk.</param>
public sealed record DeleteIntakeDeskCommand(Guid Id) : ICommand;

public sealed class DeleteIntakeDeskCommandValidator : CustomValidator<DeleteIntakeDeskCommand>
{
    public DeleteIntakeDeskCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteIntakeDeskCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IScopedWorkflowAssignments workflowAssignments,
    IDiscussionWriter discussionWriter)
    : ICommandHandler<DeleteIntakeDeskCommand>
{
    public const string HasRequestsError = "A desk with requests on it cannot be deleted. Close it instead.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IScopedWorkflowAssignments _workflowAssignments = workflowAssignments;
    private readonly IDiscussionWriter _discussionWriter = discussionWriter;

    public async Task<Result> Handle(DeleteIntakeDeskCommand request, CancellationToken cancellationToken)
    {
        var desk = await _dbContext.IntakeDesks.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (desk is null)
            return Result.Failure(IntakeDeskAccess.NotFoundError);

        if (await _dbContext.IntakeRequests.AnyAsync(r => r.DeskId == desk.Id, cancellationToken))
            return Result.Failure(HasRequestsError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var result = desk.Delete(actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        // A workflow the desk chose goes with it, or the assignment would outlive its scope and keep that
        // workflow from ever being archived. Saved below, with the desk's removal.
        var unassigned = await _workflowAssignments.RemoveScope(DemandManagementWorkflowOwners.IntakeRequest.Key, desk.Id, actor,
            _dateTimeProvider.Now, cancellationToken);
        if (unassigned.IsFailure)
            return unassigned;

        // The desk's conversation settings go with it too; nothing else reads them once the desk is gone.
        await _discussionWriter.DeleteSettings(DemandManagementDiscussionChannels.IntakeRequest.Key, desk.Id, actor, cancellationToken);

        _dbContext.IntakeDesks.Remove(desk);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(IntakeDeskAccess.StaleError);
        }

        return Result.Success();
    }
}

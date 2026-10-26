using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Points a desk at another published intake request workflow, while no request is on it.
/// </summary>
/// <remarks>
/// A Desk Admin on the desk or the Demand Management Admin, with no claim beside it: which stages a desk
/// reviews in is part of running it. A desk holding requests keeps its workflow, because moving requests
/// between workflows is a migration that is not supported yet. Choosing the workflow the desk already follows
/// changes nothing; choosing any other gives the desk an assignment of its own, after which a change to the
/// organization's default no longer reaches it.
/// </remarks>
/// <param name="Id">The desk.</param>
/// <param name="WorkflowId">A published intake request workflow.</param>
public sealed record ChangeIntakeDeskWorkflowCommand(Guid Id, Guid WorkflowId) : ICommand;

public sealed class ChangeIntakeDeskWorkflowCommandValidator : CustomValidator<ChangeIntakeDeskWorkflowCommand>
{
    public ChangeIntakeDeskWorkflowCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.WorkflowId).NotEmpty();
    }
}

public sealed class ChangeIntakeDeskWorkflowCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IScopedWorkflowAssignments workflowAssignments)
    : ICommandHandler<ChangeIntakeDeskWorkflowCommand>
{
    /// <summary>The failure for a desk that holds requests.</summary>
    public const string DeskHasRequestsError =
        "A desk's workflow can be changed only while no request is on it.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IScopedWorkflowAssignments _workflowAssignments = workflowAssignments;

    public async Task<Result> Handle(ChangeIntakeDeskWorkflowCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        if (!await _dbContext.IntakeDesks.AnyAsync(d => d.Id == request.Id, cancellationToken))
            return Result.Failure(IntakeDeskAccess.NotFoundError);

        if (!reader.IsDeskAdminOn(request.Id))
            return Result.Failure(IntakeDeskAccess.NotADeskAdminError);

        if (await _dbContext.IntakeRequests.AnyAsync(r => r.DeskId == request.Id, cancellationToken))
            return Result.Failure(DeskHasRequestsError);

        return await _workflowAssignments.AssignToEmptyScope(DemandManagementWorkflowOwners.IntakeRequest.Key, request.Id,
            request.WorkflowId, EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now, cancellationToken);
    }
}

using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Points a desk at another published intake request workflow and moves every request on the desk onto it.
/// </summary>
/// <remarks>
/// A Desk Admin on the desk or the Demand Management Admin, with no claim beside it: which stages a desk
/// reviews in is part of running it. Each request lands in the status the automatic mapping or the operator's
/// decisions send its own to (<c>GetIntakeDeskWorkflowChangeQuery</c> previews it), recorded in its status
/// history; the requests and the assignment are saved together. Choosing the workflow the desk already follows
/// moves only requests still on another one, created while an earlier change ran; choosing the organization's default makes the desk follow it again; choosing any other gives
/// the desk an assignment of its own, after which a change to the default no longer reaches it.
/// </remarks>
/// <param name="Id">The desk.</param>
/// <param name="WorkflowId">A published intake request workflow.</param>
/// <param name="Decisions">
/// Where requests in each status the automatic mapping cannot place go, and any automatic choice overridden.
/// Needed only while requests are on the desk.
/// </param>
public sealed record ChangeIntakeDeskWorkflowCommand(Guid Id, Guid WorkflowId, IReadOnlyCollection<StatusRemapDecisionDto> Decisions)
    : ICommand<int>;

public sealed class ChangeIntakeDeskWorkflowCommandValidator : CustomValidator<ChangeIntakeDeskWorkflowCommand>
{
    public ChangeIntakeDeskWorkflowCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.WorkflowId).NotEmpty();
        RuleFor(c => c.Decisions).NotNull();
    }
}

public sealed class ChangeIntakeDeskWorkflowCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IScopedWorkflowAssignments workflowAssignments)
    : ICommandHandler<ChangeIntakeDeskWorkflowCommand, int>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IScopedWorkflowAssignments _workflowAssignments = workflowAssignments;

    /// <returns>How many requests moved.</returns>
    public async Task<Result<int>> Handle(ChangeIntakeDeskWorkflowCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        if (!await _dbContext.IntakeDesks.AnyAsync(d => d.Id == request.Id, cancellationToken))
            return Result.Failure<int>(IntakeDeskAccess.NotFoundError);

        if (!reader.IsDeskAdminOn(request.Id))
            return Result.Failure<int>(IntakeDeskAccess.NotADeskAdminError);

        var changed = await _workflowAssignments.Change(DemandManagementWorkflowOwners.IntakeRequest.Key, request.Id, request.WorkflowId,
            request.Decisions, EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now, cancellationToken);

        return changed.IsFailure && changed.Error == ScopedWorkflowAssignments.ConcurrentChangeError
            ? Result.Failure<int>(IntakeDeskAccess.StaleError)
            : changed;
    }
}

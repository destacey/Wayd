using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Withdraws an open intake request: its requester no longer needs it.
/// </summary>
/// <remarks>
/// Only the request's requester, with no claim; the submitter cannot withdraw a request they entered for
/// someone else. The request moves to the status meaning Withdrawn in its desk's workflow.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Note">Why, if the requester says; the desk reads it.</param>
public sealed record WithdrawIntakeRequestCommand(Guid Id, string? Note) : ICommand, IRequireLinkedEmployee;

public sealed class WithdrawIntakeRequestCommandValidator : CustomValidator<WithdrawIntakeRequestCommand>
{
    public WithdrawIntakeRequestCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Note).MaximumLength(1024);
    }
}

public sealed class WithdrawIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IStatusResolver statusResolver)
    : ICommandHandler<WithdrawIntakeRequestCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<Result> Handle(WithdrawIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();
        var employeeId = reader.EmployeeId.Value;

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        var withdrawn = await _statusResolver.ForAlias(DemandManagementWorkflowOwners.IntakeRequest.Key, intakeRequest.DeskId,
            (int)IntakeRequestStatusAlias.Withdrawn, cancellationToken);
        if (withdrawn.IsFailure)
            return Result.Failure(withdrawn.Error);

        if (withdrawn.Value.WorkflowId != intakeRequest.StatusWorkflowId)
            return Result.Failure(IntakeRequestStatuses.WorkflowMismatchError);

        var result = intakeRequest.Withdraw(employeeId, withdrawn.Value, request.Note,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

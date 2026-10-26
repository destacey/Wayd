using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Gives an intake request a different assignee, or unassigns it.
/// </summary>
/// <remarks>
/// Needs an agent on the request's desk, and the assignee must be an active member of that desk: ownership of
/// a request belongs to the people who work its desk.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="AssigneeId">The member to assign, or null to unassign.</param>
public sealed record ChangeIntakeRequestAssigneeCommand(Guid Id, Guid? AssigneeId) : ICommand, IRequireLinkedEmployee;

public sealed class ChangeIntakeRequestAssigneeCommandValidator : CustomValidator<ChangeIntakeRequestAssigneeCommand>
{
    public ChangeIntakeRequestAssigneeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.AssigneeId).NotEqual(Guid.Empty);
    }
}

public sealed class ChangeIntakeRequestAssigneeCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeIntakeRequestAssigneeCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeIntakeRequestAssigneeCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        if (request.AssigneeId is { } assigneeId)
        {
            var isDeskMember = await _dbContext.IntakeDesks
                .AnyAsync(d => d.Id == intakeRequest.DeskId && d.Members.Any(m => m.EmployeeId == assigneeId), cancellationToken);
            if (!isDeskMember || !await _dbContext.IsActiveEmployee(assigneeId, cancellationToken))
                return Result.Failure("The assignee must be an active member of the request's desk.");
        }

        var result = intakeRequest.ChangeAssignee(request.AssigneeId,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

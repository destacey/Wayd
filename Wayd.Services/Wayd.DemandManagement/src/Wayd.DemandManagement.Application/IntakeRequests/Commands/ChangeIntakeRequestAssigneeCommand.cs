using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Gives an intake request a different assignee, or unassigns it.
/// </summary>
/// <remarks>
/// Needs an agent on the request's desk, and the assignee must be an active member of that desk: ownership of
/// a request belongs to the people who work its desk. A key in the database holds the same rule, so if the
/// assignee is removed from the desk between this check and the save, the save fails instead of leaving the
/// request owned by someone who no longer works its desk.
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

    public const string NotADeskMemberError = "The assignee must be an active member of the request's desk.";

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
                return Result.Failure(NotADeskMemberError);
        }

        var result = intakeRequest.ChangeAssignee(request.AssigneeId,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The checks above passed, so a failed save here is the assignee leaving the desk meanwhile.
            return Result.Failure(NotADeskMemberError);
        }

        return Result.Success();
    }
}

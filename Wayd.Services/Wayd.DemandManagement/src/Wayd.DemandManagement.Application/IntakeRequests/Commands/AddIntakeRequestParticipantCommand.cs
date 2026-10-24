using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Gives an employee access to an intake request as a participant.
/// </summary>
/// <remarks>
/// A reviewer on the request's desk can always add someone. The requester and the submitter can too, except
/// on a private desk that does not allow it. Participants cannot add others: on an open desk participation
/// accrues from joining the discussion, and on a private one it is granted.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="EmployeeId">The employee to give access.</param>
public sealed record AddIntakeRequestParticipantCommand(Guid Id, Guid EmployeeId) : ICommand, IRequireLinkedEmployee;

public sealed class AddIntakeRequestParticipantCommandValidator : CustomValidator<AddIntakeRequestParticipantCommand>
{
    public AddIntakeRequestParticipantCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EmployeeId).NotEmpty();
    }
}

public sealed class AddIntakeRequestParticipantCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddIntakeRequestParticipantCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(AddIntakeRequestParticipantCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is not { } employeeId)
        {
            LinkedEmployeeRequired.Throw();
            return Result.Failure(IntakeRequestAccess.NotFoundError);
        }

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: true, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.CanReview(intakeRequest.DeskId))
        {
            var isRequesterOrSubmitter = intakeRequest.RequesterId == employeeId || intakeRequest.SubmittedById == employeeId;
            var desk = await _dbContext.IntakeDesks
                .Where(d => d.Id == intakeRequest.DeskId)
                .Select(d => new { d.Visibility, d.RequesterMayAddParticipants })
                .FirstAsync(cancellationToken);
            var deskAllows = desk.Visibility != IntakeDeskVisibility.Private || desk.RequesterMayAddParticipants;

            if (!isRequesterOrSubmitter || !deskAllows)
                return Result.Failure("Only the desk's reviewers, or the requester where the desk allows it, can add participants.");
        }

        if (!await _dbContext.IsActiveEmployee(request.EmployeeId, cancellationToken))
            return Result.Failure("A participant must be an active employee.");

        var result = intakeRequest.AddParticipant(request.EmployeeId, employeeId,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

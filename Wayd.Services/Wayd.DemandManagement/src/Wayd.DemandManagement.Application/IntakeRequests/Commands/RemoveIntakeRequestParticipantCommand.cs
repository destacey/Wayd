using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Removes a participant's access to an intake request. Needs Review on the request's desk.
/// </summary>
/// <remarks>
/// Forward-only: they have already read what they read.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="EmployeeId">The participant to remove.</param>
public sealed record RemoveIntakeRequestParticipantCommand(Guid Id, Guid EmployeeId) : ICommand, IRequireLinkedEmployee;

public sealed class RemoveIntakeRequestParticipantCommandValidator : CustomValidator<RemoveIntakeRequestParticipantCommand>
{
    public RemoveIntakeRequestParticipantCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EmployeeId).NotEmpty();
    }
}

public sealed class RemoveIntakeRequestParticipantCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RemoveIntakeRequestParticipantCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RemoveIntakeRequestParticipantCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: true, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.CanReview(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAReviewerError);

        var result = intakeRequest.RemoveParticipant(request.EmployeeId,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

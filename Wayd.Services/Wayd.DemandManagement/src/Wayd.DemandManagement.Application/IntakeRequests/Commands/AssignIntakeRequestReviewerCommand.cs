using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Assigns the reviewer who owns an intake request, or unassigns it with no reviewer.
/// </summary>
/// <remarks>
/// Needs Review on the request's desk, and the assignee must be named a reviewer on that desk: ownership of
/// a request belongs to the people who review its desk.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="ReviewerId">The reviewer to assign, or null to unassign.</param>
public sealed record AssignIntakeRequestReviewerCommand(Guid Id, Guid? ReviewerId) : ICommand, IRequireLinkedEmployee;

public sealed class AssignIntakeRequestReviewerCommandValidator : CustomValidator<AssignIntakeRequestReviewerCommand>
{
    public AssignIntakeRequestReviewerCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ReviewerId).NotEqual(Guid.Empty);
    }
}

public sealed class AssignIntakeRequestReviewerCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AssignIntakeRequestReviewerCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(AssignIntakeRequestReviewerCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.CanReview(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAReviewerError);

        if (request.ReviewerId is { } reviewerId)
        {
            var isDeskReviewer = await _dbContext.IntakeDesks
                .AnyAsync(d => d.Id == intakeRequest.DeskId && d.Reviewers.Any(r => r.EmployeeId == reviewerId), cancellationToken);
            if (!isDeskReviewer || !await _dbContext.IsActiveEmployee(reviewerId, cancellationToken))
                return Result.Failure("The assigned reviewer must be an active reviewer on the request's desk.");
        }

        var result = intakeRequest.AssignReviewer(request.ReviewerId,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

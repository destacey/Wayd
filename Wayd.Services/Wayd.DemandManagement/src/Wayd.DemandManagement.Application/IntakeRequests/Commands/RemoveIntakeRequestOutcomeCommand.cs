using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Takes an outcome off an accepted intake request. Needs an agent on the request's desk.
/// </summary>
/// <remarks>
/// The project or work item itself is untouched, and an outcome whose target has since been deleted can still
/// be removed.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="TargetId">The project or work item the outcome points at.</param>
public sealed record RemoveIntakeRequestOutcomeCommand(Guid Id, Guid TargetId) : ICommand, IRequireLinkedEmployee;

public sealed class RemoveIntakeRequestOutcomeCommandValidator : CustomValidator<RemoveIntakeRequestOutcomeCommand>
{
    public RemoveIntakeRequestOutcomeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.TargetId).NotEmpty();
    }
}

public sealed class RemoveIntakeRequestOutcomeCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RemoveIntakeRequestOutcomeCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RemoveIntakeRequestOutcomeCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken,
            includeOutcomes: true);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        var result = intakeRequest.RemoveOutcome(request.TargetId,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

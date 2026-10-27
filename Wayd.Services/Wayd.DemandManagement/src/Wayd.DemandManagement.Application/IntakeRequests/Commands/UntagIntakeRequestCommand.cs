using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Takes a tag off an intake request, an inactive one included. Needs an agent on the request's desk. Succeeds
/// whether or not the request carried it.
/// </summary>
/// <param name="Id">The request.</param>
/// <param name="TagId">The tag to remove.</param>
public sealed record UntagIntakeRequestCommand(Guid Id, Guid TagId) : ICommand, IRequireLinkedEmployee;

public sealed class UntagIntakeRequestCommandValidator : CustomValidator<UntagIntakeRequestCommand>
{
    public UntagIntakeRequestCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.TagId).NotEmpty();
    }
}

public sealed class UntagIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UntagIntakeRequestCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UntagIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken, includeTags: true);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        var result = intakeRequest.Untag(request.TagId,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

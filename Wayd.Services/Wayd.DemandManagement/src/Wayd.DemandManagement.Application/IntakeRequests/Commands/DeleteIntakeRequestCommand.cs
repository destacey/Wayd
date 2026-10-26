using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Deletes an intake request: a Desk Admin's last resort, on their own desk.
/// </summary>
/// <remarks>
/// Agents cannot delete. Withdrawing is the requester's way out and declining the desk's, both of which keep
/// the request on record.
/// </remarks>
/// <param name="Id">The request.</param>
public sealed record DeleteIntakeRequestCommand(Guid Id) : ICommand;

public sealed class DeleteIntakeRequestCommandValidator : CustomValidator<DeleteIntakeRequestCommand>
{
    public DeleteIntakeRequestCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteIntakeRequestCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsDeskAdminOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotADeskAdminError);

        var hasDuplicates = await _dbContext.IntakeRequests.AnyAsync(r => r.DuplicateOfId == intakeRequest.Id, cancellationToken);

        var result = intakeRequest.Delete(hasDuplicates, EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        _dbContext.IntakeRequests.Remove(intakeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

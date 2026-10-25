using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeDesks;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Moves an open intake request to another desk.
/// </summary>
/// <remarks>
/// Needs an agent on the request's current desk; the destination need not be one the mover works, so an agent
/// can send a request to the desk that should own it. A move to a less private desk is refused unless
/// <see cref="ConfirmWiderVisibility"/> is set, so no caller widens who can read a request without being told.
/// Nothing re-routes automatically.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="DeskId">The desk to move it to.</param>
/// <param name="ConfirmWiderVisibility">Whether the mover confirmed a move that makes the request visible to more people.</param>
public sealed record RerouteIntakeRequestCommand(Guid Id, Guid DeskId, bool ConfirmWiderVisibility) : ICommand, IRequireLinkedEmployee;

public sealed class RerouteIntakeRequestCommandValidator : CustomValidator<RerouteIntakeRequestCommand>
{
    public RerouteIntakeRequestCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.DeskId).NotEmpty();
    }
}

public sealed class RerouteIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RerouteIntakeRequestCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RerouteIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        var from = await _dbContext.IntakeDesks.FirstAsync(d => d.Id == intakeRequest.DeskId, cancellationToken);
        var to = await _dbContext.IntakeDesks
            .Include(d => d.Members)
            .FirstOrDefaultAsync(d => d.Id == request.DeskId, cancellationToken);
        if (to is null)
            return Result.Failure(IntakeDeskAccess.NotFoundError);

        var result = intakeRequest.Reroute(from, to, request.ConfirmWiderVisibility,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

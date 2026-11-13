using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Adds a round about each request chosen to a planning poker session the desk hosts.
/// </summary>
/// <remarks>
/// An agent's action on the desk. Planning refuses a session the desk does not host, and a request that already
/// has a round in it. Each round shares its request's key, title and description with the session's participants.
/// </remarks>
/// <param name="DeskId">The desk hosting the session.</param>
/// <param name="SessionId">The session.</param>
/// <param name="RequestIds">Open requests on the desk to estimate, one round each, in this order.</param>
public sealed record AddIntakeRequestsToPokerSessionCommand(Guid DeskId, Guid SessionId, IReadOnlyList<Guid> RequestIds)
    : ICommand, IRequireLinkedEmployee;

public sealed class AddIntakeRequestsToPokerSessionCommandValidator : CustomValidator<AddIntakeRequestsToPokerSessionCommand>
{
    public AddIntakeRequestsToPokerSessionCommandValidator()
    {
        RuleFor(c => c.DeskId).NotEmpty();
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RequestIds).NotEmpty();
        RuleForEach(c => c.RequestIds).NotEmpty();
    }
}

/// <summary>
/// Checks the caller works the desk and the requests are open ones on it, then asks Planning to add the rounds.
/// </summary>
public sealed class AddIntakeRequestsToPokerSessionCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IDispatcher dispatcher) : ICommandHandler<AddIntakeRequestsToPokerSessionCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result> Handle(AddIntakeRequestsToPokerSessionCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var desk = await _dbContext.FindDeskToEstimateOn(reader, request.DeskId, cancellationToken);
        if (desk.IsFailure)
            return Result.Failure(desk.Error);

        var subjects = await _dbContext.PokerRoundSubjects(desk.Value.Id, request.RequestIds, cancellationToken);
        if (subjects.IsFailure)
            return Result.Failure(subjects.Error);

        return await _dispatcher.Send(
            new AddPokerRoundSubjectsCommand(request.SessionId, PokerSessionHostKind.IntakeDesk, desk.Value.Id, subjects.Value),
            cancellationToken);
    }
}

using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Starts a planning poker session hosted by an intake desk, with a round about each request chosen, voting on the
/// estimation scale version the desk pins.
/// </summary>
/// <remarks>
/// An agent's action on the desk, with no claim beside it but the poker View permission Planning asks of anyone
/// opening a session. The session is private, every agent and Desk Admin on the desk facilitates it, and each
/// round shares its request's key, title and description with the session's participants.
/// </remarks>
/// <param name="DeskId">The desk hosting the session.</param>
/// <param name="Name">The session's name.</param>
/// <param name="RequestIds">Open requests on the desk to estimate, one round each, in this order. May be empty.</param>
/// <param name="InvitedEmployeeIds">Employees invited to vote, beside the desk's members.</param>
public sealed record StartIntakeDeskPokerSessionCommand(
    Guid DeskId,
    string Name,
    IReadOnlyList<Guid> RequestIds,
    IReadOnlyCollection<Guid>? InvitedEmployeeIds = null) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

public sealed class StartIntakeDeskPokerSessionCommandValidator : CustomValidator<StartIntakeDeskPokerSessionCommand>
{
    public StartIntakeDeskPokerSessionCommandValidator()
    {
        RuleFor(c => c.DeskId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);
        RuleFor(c => c.RequestIds).NotNull();
        RuleForEach(c => c.RequestIds).NotEmpty();
    }
}

/// <summary>
/// Checks the caller works the desk and the requests are open ones on it, then asks Planning to start the session.
/// </summary>
public sealed class StartIntakeDeskPokerSessionCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IDispatcher dispatcher) : ICommandHandler<StartIntakeDeskPokerSessionCommand, ObjectIdAndKey>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<ObjectIdAndKey>> Handle(StartIntakeDeskPokerSessionCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var desk = await _dbContext.FindDeskToEstimateOn(reader, request.DeskId, cancellationToken);
        if (desk.IsFailure)
            return Result.Failure<ObjectIdAndKey>(desk.Error);

        var subjects = await _dbContext.PokerRoundSubjects(desk.Value.Id, request.RequestIds, cancellationToken);
        if (subjects.IsFailure)
            return Result.Failure<ObjectIdAndKey>(subjects.Error);

        return await _dispatcher.Send(
            new StartHostedPokerSessionCommand(request.Name, PokerSessionHostKind.IntakeDesk, desk.Value.Id,
                desk.Value.EstimationScaleVersionId!.Value, subjects.Value, request.InvitedEmployeeIds),
            cancellationToken);
    }
}

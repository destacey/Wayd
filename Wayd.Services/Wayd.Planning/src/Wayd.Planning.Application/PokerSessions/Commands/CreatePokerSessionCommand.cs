using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Start a poker session facilitated by the caller.
/// </summary>
/// <param name="Visibility">Public, which anyone with the poker View permission can join, or Private.</param>
/// <param name="FacilitatorIds">Employees who facilitate alongside the caller, who is always the first.</param>
/// <param name="InvitedEmployeeIds">Employees invited to the session.</param>
/// <param name="InvitedTeamIds">Teams whose members are invited to the session.</param>
public sealed record CreatePokerSessionCommand(
    string Name,
    Guid EstimationScaleId,
    Visibility Visibility = Visibility.Public,
    IReadOnlyCollection<Guid>? FacilitatorIds = null,
    IReadOnlyCollection<Guid>? InvitedEmployeeIds = null,
    IReadOnlyCollection<Guid>? InvitedTeamIds = null) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

public sealed class CreatePokerSessionCommandValidator : CustomValidator<CreatePokerSessionCommand>
{
    public CreatePokerSessionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(c => c.EstimationScaleId)
            .NotEmpty();

        RuleFor(c => c.Visibility)
            .IsInEnum();
    }
}

public sealed class CreatePokerSessionCommandHandler(
    IPlanningDbContext planningDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreatePokerSessionCommandHandler> logger) : ICommandHandler<CreatePokerSessionCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreatePokerSessionCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreatePokerSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            if (employeeId is null)
                LinkedEmployeeRequired.Throw();

            var scale = await _planningDbContext.EstimationScales
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == request.EstimationScaleId, cancellationToken);

            if (scale is null)
                return Result.Failure<ObjectIdAndKey>("Estimation scale not found.");

            IReadOnlyCollection<Guid> facilitatorIds = [employeeId.Value, .. request.FacilitatorIds ?? []];
            var invitedEmployeeIds = request.InvitedEmployeeIds ?? [];
            var invitedTeamIds = request.InvitedTeamIds ?? [];

            var exist = Result.Combine(
                await _planningDbContext.EnsureEmployeesExist([.. facilitatorIds, .. invitedEmployeeIds], cancellationToken),
                await _planningDbContext.EnsureTeamsExist(invitedTeamIds, cancellationToken));
            if (exist.IsFailure)
                return Result.Failure<ObjectIdAndKey>(exist.Error);

            var sessionResult = PokerSession.Create(
                request.Name,
                scale,
                request.Visibility,
                employeeId.Value,
                _dateTimeProvider.Now);

            if (sessionResult.IsFailure)
                return Result.Failure<ObjectIdAndKey>(sessionResult.Error);

            var session = sessionResult.Value;

            // The creator facilitates the new session, which is all these two need.
            var creator = new PokerSessionActor(employeeId.Value, _currentUser.GetUserId(), false, new HashSet<Guid>());
            var setUp = Result.Combine(
                session.SetFacilitators(facilitatorIds, creator),
                session.SetInvitees(invitedEmployeeIds, invitedTeamIds, creator));
            if (setUp.IsFailure)
                return Result.Failure<ObjectIdAndKey>(setUp.Error);

            await _planningDbContext.PokerSessions.AddAsync(session, cancellationToken);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(session.Id, session.Key);
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}

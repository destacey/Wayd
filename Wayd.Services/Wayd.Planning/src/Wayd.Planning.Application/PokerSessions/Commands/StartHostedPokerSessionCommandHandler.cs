using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Checks the session's name, host, version and records before the handler loads anything.
/// </summary>
public sealed class StartHostedPokerSessionCommandValidator : CustomValidator<StartHostedPokerSessionCommand>
{
    public StartHostedPokerSessionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);
        RuleFor(c => c.HostKind).IsInEnum();
        RuleFor(c => c.HostId).NotEmpty();
        RuleFor(c => c.EstimationScaleVersionId).NotEmpty();
        RuleFor(c => c.Subjects)
            .NotNull()
            .Must(s => s.Count <= PokerRoundSubjectInputValidator.MaxSubjectsPerRequest)
                .WithMessage($"Add at most {PokerRoundSubjectInputValidator.MaxSubjectsPerRequest} records at a time.");
        RuleForEach(c => c.Subjects).SetValidator(new PokerRoundSubjectInputValidator());
    }
}

/// <summary>
/// Checks the caller manages the host, then starts the session on the host's scale version with its rounds.
/// </summary>
public sealed class StartHostedPokerSessionCommandHandler(
    IPlanningDbContext planningDbContext,
    IPokerSessionAccess access,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<StartHostedPokerSessionCommand, ObjectIdAndKey>
{
    /// <summary>
    /// The failure for a caller who does not manage the host they are starting a session for.
    /// </summary>
    public const string NotAHostManagerError = "Only those who manage this area can start a poker session for it.";

    /// <summary>
    /// The failure for a caller without the poker View permission, who could not open the session they started.
    /// </summary>
    public const string NoPokerAccessError = "Starting a poker session needs the View Poker Sessions permission.";

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <summary>
    /// Refuses a caller who could not open the session or does not manage the host, then creates it with its
    /// invites and rounds in one save.
    /// </summary>
    public async Task<Result<ObjectIdAndKey>> Handle(StartHostedPokerSessionCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        if (!await _currentPrincipal.HasPermission(PokerSessionAccess.ViewPermission, cancellationToken))
            return Result.Failure<ObjectIdAndKey>(NoPokerAccessError);

        var host = new PokerSessionHost(request.HostKind, request.HostId);
        if (!await _access.ManagesHost(host, employeeId.Value, cancellationToken))
            return Result.Failure<ObjectIdAndKey>(NotAHostManagerError);

        var version = await _planningDbContext.EstimationScaleVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.EstimationScaleVersionId, cancellationToken);
        if (version is null)
            return Result.Failure<ObjectIdAndKey>("Estimation scale version not found.");

        var invitedEmployeeIds = request.InvitedEmployeeIds ?? [];
        var exist = await _planningDbContext.EnsureEmployeesExist(invitedEmployeeIds, cancellationToken);
        if (exist.IsFailure)
            return Result.Failure<ObjectIdAndKey>(exist.Error);

        var created = PokerSession.CreateHosted(request.Name, version, host, employeeId.Value, _dateTimeProvider.Now);
        if (created.IsFailure)
            return Result.Failure<ObjectIdAndKey>(created.Error);

        var session = created.Value;
        var creator = new PokerSessionActor(employeeId.Value, _currentUser.GetUserId(), false, new HashSet<Guid>(), ManagesHost: true);
        var setUp = Result.Combine(
            session.SetInvitees(invitedEmployeeIds, [], creator),
            session.AddSubjectRounds(request.Subjects.Select(PokerRoundSubjectInputs.ToRecord), creator));
        if (setUp.IsFailure)
            return Result.Failure<ObjectIdAndKey>(setUp.Error);

        await _planningDbContext.PokerSessions.AddAsync(session, cancellationToken);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return new ObjectIdAndKey(session.Id, session.Key);
    }
}

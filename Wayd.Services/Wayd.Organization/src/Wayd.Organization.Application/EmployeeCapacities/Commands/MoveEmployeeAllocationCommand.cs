using NodaTime;

namespace Wayd.Organization.Application.EmployeeCapacities.Commands;

/// <summary>
/// Moves a share of an employee's time from one team and role to another over a date range, as one change.
/// Allowed for members of both teams, or of their teams of teams, and for a teams administrator.
/// </summary>
/// <param name="End">The last day of the move, or null for every day from <paramref name="Start"/> on.</param>
/// <param name="Percent">The share of the employee's time to move, from 1 to 100.</param>
public sealed record MoveEmployeeAllocationCommand(
    Guid EmployeeId,
    Guid FromTeamId,
    Guid? FromRoleId,
    Guid ToTeamId,
    Guid? ToRoleId,
    LocalDate Start,
    LocalDate? End,
    int Percent) : ICommand;

public sealed class MoveEmployeeAllocationCommandValidator : CustomValidator<MoveEmployeeAllocationCommand>
{
    public MoveEmployeeAllocationCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.EmployeeId)
            .NotEmpty();

        RuleFor(c => c.FromTeamId)
            .NotEmpty();

        RuleFor(c => c.ToTeamId)
            .NotEmpty();

        RuleFor(c => c.Percent)
            .InclusiveBetween(1, EmployeeCapacity.FullAllocation);

        RuleFor(c => c.End)
            .GreaterThanOrEqualTo(c => c.Start)
            .When(c => c.End.HasValue)
            .WithMessage("An allocation cannot end before it starts.");
    }
}

public sealed class MoveEmployeeAllocationCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDispatcher dispatcher,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<MoveEmployeeAllocationCommandHandler> logger)
    : ICommandHandler<MoveEmployeeAllocationCommand>
{
    private const string AppRequestName = nameof(MoveEmployeeAllocationCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<MoveEmployeeAllocationCommandHandler> _logger = logger;

    public async Task<Result> Handle(MoveEmployeeAllocationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            List<AllocationTarget> targets =
            [
                new AllocationTarget(request.FromTeamId, request.FromRoleId, Allocates: false),
                new AllocationTarget(request.ToTeamId, request.ToRoleId, Allocates: true),
            ];

            var validation = await AllocationTargets.Validate(_organizationDbContext, request.EmployeeId, targets, cancellationToken);
            if (validation.IsFailure)
                return validation;

            var now = _dateTimeProvider.Now;
            List<Guid> teamIds = [.. targets.Select(t => t.TeamId).Distinct()];
            var manageable = await _currentPrincipal.TeamsWithManageableAllocations(_dispatcher, teamIds, now.InUtc().Date, cancellationToken);
            if (!teamIds.TrueForAll(manageable.Contains))
                return Result.Failure(TeamAllocationAuthorization.NotAMemberError);

            var capacity = await AllocationTargets.LoadOrCreateCapacity(_organizationDbContext, request.EmployeeId, cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var result = capacity.MoveAllocation(
                request.FromTeamId,
                request.FromRoleId,
                request.ToTeamId,
                request.ToRoleId,
                request.Start,
                request.End,
                request.Percent,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                now);
            if (result.IsFailure)
                return result;

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(AllocationTargets.ChangedElsewhereError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

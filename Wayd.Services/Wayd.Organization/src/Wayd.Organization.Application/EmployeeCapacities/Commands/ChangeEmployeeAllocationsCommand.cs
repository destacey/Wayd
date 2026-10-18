namespace Wayd.Organization.Application.EmployeeCapacities.Commands;

/// <summary>
/// Sets the share of an employee's time one or more teams have over date ranges, all together or not at all.
/// Allowed for members of every team named, or of its team of teams, and for a teams administrator.
/// </summary>
public sealed record ChangeEmployeeAllocationsCommand(Guid EmployeeId, IReadOnlyList<AllocationChange> Changes) : ICommand;

public sealed class ChangeEmployeeAllocationsCommandValidator : CustomValidator<ChangeEmployeeAllocationsCommand>
{
    public ChangeEmployeeAllocationsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.EmployeeId)
            .NotEmpty();

        RuleFor(c => c.Changes)
            .NotEmpty();

        RuleForEach(c => c.Changes).ChildRules(change =>
        {
            change.RuleFor(c => c.TeamId)
                .NotEmpty();

            change.RuleFor(c => c.Percent)
                .InclusiveBetween(0, EmployeeCapacity.FullAllocation);

            change.RuleFor(c => c.End)
                .GreaterThanOrEqualTo(c => c.Start)
                .When(c => c.End.HasValue)
                .WithMessage("An allocation cannot end before it starts.");
        });
    }
}

public sealed class ChangeEmployeeAllocationsCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDispatcher dispatcher,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<ChangeEmployeeAllocationsCommandHandler> logger)
    : ICommandHandler<ChangeEmployeeAllocationsCommand>
{
    private const string AppRequestName = nameof(ChangeEmployeeAllocationsCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ChangeEmployeeAllocationsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ChangeEmployeeAllocationsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            List<AllocationTarget> targets = [.. request.Changes.Select(c => new AllocationTarget(c.TeamId, c.RoleId, c.Percent > 0, c.End is null))];

            var validation = await AllocationTargets.Validate(_organizationDbContext, request.EmployeeId, targets, cancellationToken);
            if (validation.IsFailure)
                return validation;

            var now = _dateTimeProvider.Now;
            List<Guid> teamIds = [.. targets.Select(t => t.TeamId).Distinct()];
            var manageable = await _currentPrincipal.TeamsWithManageableAllocations(_dispatcher, teamIds, now.InUtc().Date, cancellationToken);
            if (!teamIds.TrueForAll(manageable.Contains))
                return Result.Failure(TeamAllocationAuthorization.NotAMemberError);

            var (capacity, isNew) = await AllocationTargets.LoadOrCreateCapacity(_organizationDbContext, request.EmployeeId, cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var result = capacity.ChangeAllocations(request.Changes, EventActor.User(_currentUser.GetUserId(), employeeId), now);
            if (result.IsFailure)
                return result;

            return await AllocationTargets.Save(_organizationDbContext, capacity, isNew, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record UpdateTeamMemberCommand(Guid TeamId, Guid EmployeeId, IReadOnlyList<Guid> RoleIds) : ICommand;

public sealed class UpdateTeamMemberCommandValidator : CustomValidator<UpdateTeamMemberCommand>
{
    public UpdateTeamMemberCommandValidator()
    {
        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.EmployeeId).NotEmpty();
        RuleFor(c => c.RoleIds).NotEmpty();
        RuleForEach(c => c.RoleIds).NotEmpty();
    }
}

public sealed class UpdateTeamMemberCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IWaydDbContext waydDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<UpdateTeamMemberCommandHandler> logger)
    : ICommandHandler<UpdateTeamMemberCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateTeamMemberCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var team = await _organizationDbContext.BaseTeams
            .Include(t => t.Members)
            .SingleOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
        if (team is null)
        {
            _logger.LogInformation("Team with Id {TeamId} not found.", request.TeamId);
            return Result.Failure("Team not found.");
        }

        var employee = await _waydDbContext.Employees
            .SingleOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            _logger.LogInformation("Employee with Id {EmployeeId} not found.", request.EmployeeId);
            return Result.Failure("Employee not found.");
        }

        var heldRoleIds = team.Members
            .Where(m => m.EmployeeId == request.EmployeeId && !m.IsDeleted)
            .Select(m => m.RoleId)
            .ToHashSet();

        var rolesCheck = await TeamMemberRoleIds.CheckAssignable(_organizationDbContext, request.RoleIds, heldRoleIds, cancellationToken);
        if (rolesCheck.IsFailure)
            return rolesCheck;

        var result = team.UpdateMemberRoles(employee, request.RoleIds, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
        if (result.IsFailure)
        {
            _logger.LogError("Error updating roles for employee {EmployeeId} on team {TeamId}. Error: {Error}", request.EmployeeId, request.TeamId, result.Error);
            return result;
        }

        await _organizationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team member {EmployeeId} updated on team {TeamId}.", request.EmployeeId, request.TeamId);

        return Result.Success();
    }
}

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record AddTeamMemberCommand(Guid TeamId, Guid EmployeeId, IReadOnlyList<Guid> RoleIds) : ICommand;

public sealed class AddTeamMemberCommandValidator : CustomValidator<AddTeamMemberCommand>
{
    public AddTeamMemberCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.EmployeeId).NotEmpty();
        RuleFor(c => c.RoleIds).NotEmpty();
        RuleForEach(c => c.RoleIds).NotEmpty();
    }
}

public sealed class AddTeamMemberCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IWaydDbContext waydDbContext,
    ILogger<AddTeamMemberCommandHandler> logger)
    : ICommandHandler<AddTeamMemberCommand>
{
    private const string AppRequestName = nameof(AddTeamMemberCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<AddTeamMemberCommandHandler> _logger = logger;

    public async Task<Result> Handle(AddTeamMemberCommand request, CancellationToken cancellationToken)
    {
        try
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

            var result = team.AddMember(employee, request.RoleIds);
            if (result.IsFailure)
            {
                _logger.LogError("Error adding member to team {TeamId}. Error message: {Error}", request.TeamId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Employee {EmployeeId} added to team {TeamId} with {RoleCount} role(s).", request.EmployeeId, request.TeamId, request.RoleIds.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

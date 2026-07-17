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
    ILogger<UpdateTeamMemberCommandHandler> logger)
    : ICommandHandler<UpdateTeamMemberCommand>
{
    private const string AppRequestName = nameof(UpdateTeamMemberCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<UpdateTeamMemberCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTeamMemberCommand request, CancellationToken cancellationToken)
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

            var result = team.UpdateMemberRoles(employee, request.RoleIds);
            if (result.IsFailure)
            {
                _logger.LogError("Error updating roles for employee {EmployeeId} on team {TeamId}. Error: {Error}", request.EmployeeId, request.TeamId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Team member {EmployeeId} updated on team {TeamId}.", request.EmployeeId, request.TeamId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
